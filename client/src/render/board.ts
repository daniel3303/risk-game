import { ArcRotateCamera } from '@babylonjs/core/Cameras/arcRotateCamera';
import { Camera } from '@babylonjs/core/Cameras/camera';
import { Engine } from '@babylonjs/core/Engines/engine';
import { PointerEventTypes } from '@babylonjs/core/Events/pointerEvents';
import { DirectionalLight } from '@babylonjs/core/Lights/directionalLight';
import { HemisphericLight } from '@babylonjs/core/Lights/hemisphericLight';
import { ShadowGenerator } from '@babylonjs/core/Lights/Shadows/shadowGenerator';
import { GlowLayer } from '@babylonjs/core/Layers/glowLayer';
import { StandardMaterial } from '@babylonjs/core/Materials/standardMaterial';
import { Color3, Color4 } from '@babylonjs/core/Maths/math.color';
import { Matrix, Vector2, Vector3 } from '@babylonjs/core/Maths/math.vector';
import { Mesh } from '@babylonjs/core/Meshes/mesh';
import { MeshBuilder } from '@babylonjs/core/Meshes/meshBuilder';
import { PolygonMeshBuilder } from '@babylonjs/core/Meshes/polygonMesh';
import { Scene } from '@babylonjs/core/scene';
import '@babylonjs/core/Lights/Shadows/shadowGeneratorSceneComponent';
import '@babylonjs/core/Rendering/edgesRenderer';
import earcut from 'earcut';
import { map, playerColors } from '../game/map';
import type { GameView } from '../game/types';
import { createOcean } from './ocean';
import { createInfantry } from './troops';
import { createSeaRoutes } from './routes';
import { createReliefTexture, landHeight, seaHeight } from './terrain';

interface TerritoryArt { faces: Mesh[]; material: StandardMaterial; unit: Mesh; ring: Mesh }
const latitudes = map.territories.flatMap(t => (t.parts ?? [t.shape]).flat().map(point => point[1]));
const mapCenterZ = (Math.min(...latitudes) + Math.max(...latitudes)) / 2;
const mapHalfHeight = (Math.max(...latitudes) - Math.min(...latitudes)) / 2 + 1.2;

export class Board {
  private engine: Engine;
  private scene: Scene;
  private camera: ArcRotateCamera;
  private territories: TerritoryArt[] = [];
  private resize: ResizeObserver;
  private labels: HTMLElement[];
  private continentLabels: HTMLElement[];
  private relief: ReturnType<typeof createReliefTexture>;
  private glow: GlowLayer;
  private needsRender = true;
  private cameraSettlingUntil = 0;
  private lastFrame = 0;
  private wakeCamera = () => { this.needsRender = true; this.cameraSettlingUntil = performance.now() + 1200; };

  constructor(private canvas: HTMLCanvasElement, labelLayer: HTMLElement, onSelect: (id: number) => void, preview: boolean) {
    this.engine = new Engine(canvas, true, { stencil: true, preserveDrawingBuffer: true });
    this.engine.setHardwareScalingLevel(Math.max(1, window.devicePixelRatio / 1.5));
    this.scene = new Scene(this.engine);
    this.scene.clearColor = new Color4(.03, .32, .46, 1);
    this.scene.skipPointerMovePicking = true;
    this.camera = new ArcRotateCamera('camera', -Math.PI / 2, .26, 42, new Vector3(0, 0, mapCenterZ), this.scene);
    this.camera.mode = Camera.ORTHOGRAPHIC_CAMERA;
    this.camera.lowerBetaLimit = .05; this.camera.upperBetaLimit = .65;
    this.camera.lowerRadiusLimit = 16; this.camera.upperRadiusLimit = 70;
    this.camera.wheelPrecision = 15; this.camera.panningSensibility = 110;
    if (!preview) this.camera.attachControl(canvas, true);
    const ambient = new HemisphericLight('ambient', new Vector3(0, 1, 0), this.scene);
    ambient.intensity = .78; ambient.groundColor = Color3.FromHexString('#243645');
    const sun = new DirectionalLight('sun', new Vector3(-.4, -1.5, .6), this.scene);
    sun.position.set(12, 30, -15); sun.intensity = .48;
    const shadows = new ShadowGenerator(512, sun);
    shadows.usePercentageCloserFiltering = true; shadows.darkness = .2;
    this.labels = Array.from(labelLayer.querySelectorAll<HTMLElement>('[data-map-label]'));
    this.continentLabels = Array.from(labelLayer.querySelectorAll<HTMLElement>('[data-continent-label]'));
    this.relief = createReliefTexture(this.scene);
    this.glow = new GlowLayer('coast-glow', this.scene, { mainTextureRatio: .25, blurKernelSize: 16 });
    this.glow.intensity = .6;
    this.glow.customEmissiveColorSelector = (mesh, _subMesh, _material, color) => {
      const coast = mesh.name.startsWith('coast-');
      color.set(coast ? .02 : 0, coast ? .75 : 0, coast ? 1 : 0, 1);
    };
    createOcean(this.scene);
    createSeaRoutes(this.scene);
    for (const territory of map.territories) this.createTerritory(territory.id, shadows);
    this.pointerInput(onSelect);
    canvas.addEventListener('wheel', this.wakeCamera, { passive: true });
    this.resize = new ResizeObserver(() => { this.engine.resize(); this.needsRender = true; });
    this.resize.observe(canvas);
    this.update(null, null, []);
    this.engine.runRenderLoop(() => {
      const now = performance.now();
      if ((!this.needsRender && now > this.cameraSettlingUntil) || now - this.lastFrame < 1000 / 30) return;
      this.lastFrame = now;
      this.cameraBounds();
      this.scene.render();
      this.positionLabels();
      this.needsRender = !this.scene.isReady();
      if (!this.needsRender) canvas.dataset.ready = 'true';
    });
  }

  private material(name: string, color: string, unlit = false) {
    const material = new StandardMaterial(name, this.scene);
    material.diffuseColor = Color3.FromHexString(color).toLinearSpace();
    material.specularColor = Color3.Black();
    if (unlit) { material.disableLighting = true; material.emissiveColor = material.diffuseColor; }
    return material;
  }

  private polygon(name: string, points: number[][], y: number, depth: number, material: StandardMaterial) {
    const mesh = new PolygonMeshBuilder(name, points.map(p => new Vector2(p[0], p[1])), this.scene, earcut).build(false, depth);
    mesh.position.y = y; mesh.material = material;
    return mesh;
  }

  private createTerritory(id: number, shadows: ShadowGenerator) {
    const territory = map.territories[id];
    const color = map.continents.find(c => c.id === territory.continent)!.color;
    const material = this.material(`land-${id}`, color);
    material.diffuseTexture = this.relief;
    const edge = this.material(`edge-${id}`, '#05090b', true);
    const coast = this.material(`coast-${id}`, '#1bd5ef', true);
    coast.alpha = .6;
    const faces: Mesh[] = [];
    for (const [index, points] of (territory.parts ?? [territory.shape]).entries()) {
      const center = points.reduce((a, p) => [a[0] + p[0] / points.length, a[1] + p[1] / points.length], [0, 0]);
      const scale = (factor: number) => points.map(p => [center[0] + (p[0] - center[0]) * factor, center[1] + (p[1] - center[1]) * factor]);
      const halo = this.polygon(`coast-${id}-${index}`, scale(1.04), seaHeight + .1, .02, coast);
      halo.isPickable = false;
      const base = this.polygon(`border-${id}-${index}`, points, landHeight - .24, 1.05, edge);
      base.metadata = { territory: id };
      const face = this.polygon(`land-${id}-${index}`, scale(.945), landHeight, .18, material);
      face.metadata = { territory: id }; face.receiveShadows = true;
      face.enableEdgesRendering(); face.edgesWidth = 2; face.edgesColor = new Color4(.025, .04, .04, .9);
      faces.push(face);
      if (id === 0 || id === 29) for (const mesh of [halo, base, face]) {
        const copy = mesh.clone(`${mesh.name}-wrap`)!;
        copy.position.x += id === 0 ? 48 : -48;
        copy.isPickable = false;
      }
    }
    const unit = createInfantry(this.scene, id);
    unit.position.set(territory.x + .9, landHeight, territory.z + .15);
    unit.rotation.y = -.35;
    shadows.addShadowCaster(unit);
    const ring = MeshBuilder.CreateTorus(`selection-${id}`, { diameter: 1.2, thickness: .08, tessellation: 36 }, this.scene);
    ring.position.set(territory.x, landHeight + .05, territory.z);
    ring.material = this.material(`selection-color-${id}`, '#fff5a6', true);
    ring.isPickable = false; ring.setEnabled(false);
    this.territories.push({ faces, material, unit, ring });
  }

  private pointerInput(onSelect: (id: number) => void) {
    let downX = 0, downY = 0;
    this.scene.onPointerObservable.add(info => {
      this.wakeCamera();
      if (info.type === PointerEventTypes.POINTERDOWN) { downX = info.event.clientX; downY = info.event.clientY; }
      if (info.type !== PointerEventTypes.POINTERUP || Math.hypot(info.event.clientX - downX, info.event.clientY - downY) > 8) return;
      const id = info.pickInfo?.pickedMesh?.metadata?.territory as number | undefined;
      if (id !== undefined) onSelect(id);
    });
  }

  update(game: GameView | null, selected: number | null, reachable: number[], continentOverlay = false, viewer = -1) {
    this.needsRender = true;
    for (const territory of map.territories) {
      const art = this.territories[territory.id];
      const state = game?.territories[territory.id];
      const continentColor = map.continents.find(c => c.id === territory.continent)!.color;
      const color = !continentOverlay && state && state.owner >= 0 ? playerColors[state.owner] : continentColor;
      const brightness = continentOverlay && state && state.owner !== viewer ? .35 : selected === territory.id ? 1.05 : .74;
      art.material.diffuseColor = Color3.FromHexString(color).toLinearSpace().scale(brightness);
      art.material.emissiveColor = Color3.FromHexString(color).toLinearSpace().scale(reachable.includes(territory.id) ? .23 : .025);
      art.ring.setEnabled(selected === territory.id || reachable.includes(territory.id));
      art.unit.setEnabled(selected === territory.id && !continentOverlay);
      for (const face of art.faces) {
        face.edgesWidth = selected === territory.id ? 3 : 2;
        face.edgesColor = selected === territory.id ? new Color4(1, 1, .9, 1) : new Color4(.025, .04, .04, .9);
      }
    }
  }

  private cameraBounds() {
    const ratio = this.engine.getAspectRatio(this.camera);
    const wide = this.canvas.clientWidth > 1100;
    const height = Math.max(mapHalfHeight, 24 / (ratio * (wide ? .78 : 1))) * this.camera.radius / 42;
    const width = height * ratio;
    const shift = wide ? width * .015 : 0;
    this.camera.orthoLeft = -width + shift; this.camera.orthoRight = width + shift;
    this.camera.orthoTop = width / ratio; this.camera.orthoBottom = -width / ratio;
  }

  private positionLabels() {
    const viewport = this.camera.viewport.toGlobal(this.canvas.clientWidth, this.canvas.clientHeight);
    const transform = this.scene.getTransformMatrix();
    for (const territory of map.territories) {
      const label = this.labels[territory.id];
      if (!label) continue;
      const position = Vector3.Project(new Vector3(territory.x, landHeight + .2, territory.z), Matrix.IdentityReadOnly, transform, viewport);
      const left = `${Math.round(position.x)}px`, top = `${Math.round(position.y)}px`;
      if (label.style.left !== left) label.style.left = left;
      if (label.style.top !== top) label.style.top = top;
    }
    for (const [i, continent] of map.continents.entries()) {
      const region = map.territories.filter(t => t.continent === continent.id);
      const x = region.reduce((sum, t) => sum + t.x / region.length, 0), z = region.reduce((sum, t) => sum + t.z / region.length, 0);
      const point = Vector3.Project(new Vector3(x, .5, z), Matrix.IdentityReadOnly, transform, viewport);
      const label = this.continentLabels[i];
      if (label) { label.style.left = `${Math.round(point.x)}px`; label.style.top = `${Math.round(point.y)}px`; }
    }
  }

  zoom(direction: number) { this.camera.radius = Math.max(16, Math.min(70, this.camera.radius + direction * 5)); this.wakeCamera(); }
  resetCamera() { this.camera.alpha = -Math.PI / 2; this.camera.beta = .26; this.camera.radius = 42; this.camera.target.set(0, 0, mapCenterZ); this.wakeCamera(); }
  dispose() { this.canvas.removeEventListener('wheel', this.wakeCamera); this.resize.disconnect(); this.scene.dispose(); this.engine.dispose(); }
}

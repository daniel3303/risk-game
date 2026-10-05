import { ArcRotateCamera } from '@babylonjs/core/Cameras/arcRotateCamera';
import { Engine } from '@babylonjs/core/Engines/engine';
import { PointerEventTypes } from '@babylonjs/core/Events/pointerEvents';
import { DirectionalLight } from '@babylonjs/core/Lights/directionalLight';
import { HemisphericLight } from '@babylonjs/core/Lights/hemisphericLight';
import { ShadowGenerator } from '@babylonjs/core/Lights/Shadows/shadowGenerator';
import { PBRMaterial } from '@babylonjs/core/Materials/PBR/pbrMaterial';
import { DynamicTexture } from '@babylonjs/core/Materials/Textures/dynamicTexture';
import { StandardMaterial } from '@babylonjs/core/Materials/standardMaterial';
import { Color3, Color4 } from '@babylonjs/core/Maths/math.color';
import { Vector2, Vector3 } from '@babylonjs/core/Maths/math.vector';
import { Mesh } from '@babylonjs/core/Meshes/mesh';
import { MeshBuilder } from '@babylonjs/core/Meshes/meshBuilder';
import { PolygonMeshBuilder } from '@babylonjs/core/Meshes/polygonMesh';
import { Scene } from '@babylonjs/core/scene';
import '@babylonjs/core/Lights/Shadows/shadowGeneratorSceneComponent';
import '@babylonjs/core/Rendering/edgesRenderer';
import earcut from 'earcut';
import { map, playerColors } from '../game/map';
import type { GameView } from '../game/types';

interface TerritoryArt { mesh: Mesh; material: PBRMaterial; label: DynamicTexture; tag: Mesh; unit: Mesh; ring: Mesh; last: string }

export class Board {
  private engine: Engine;
  private scene: Scene;
  private camera: ArcRotateCamera;
  private territories: TerritoryArt[] = [];
  private resize: ResizeObserver;
  private selected: number | null = null;
  private reachable: number[] = [];
  private game: GameView | null = null;
  private elapsed = 0;

  constructor(canvas: HTMLCanvasElement, onSelect: (id: number) => void, preview: boolean) {
    this.engine = new Engine(canvas, true, { preserveDrawingBuffer: true, stencil: true });
    this.engine.setHardwareScalingLevel(Math.max(1, window.devicePixelRatio / 1.5));
    this.scene = new Scene(this.engine);
    this.scene.clearColor = new Color4(0.055, 0.085, 0.072, 1);
    this.scene.ambientColor = new Color3(0.6, 0.65, 0.58);
    this.camera = new ArcRotateCamera('camera', -Math.PI / 2, 0.64, 38, new Vector3(1.2, 0, 0), this.scene);
    this.camera.lowerBetaLimit = 0.15;
    this.camera.upperBetaLimit = 1.05;
    this.camera.lowerRadiusLimit = 14;
    this.camera.upperRadiusLimit = 85;
    this.camera.wheelPrecision = 20;
    this.camera.panningSensibility = 150;
    this.camera.attachControl(canvas, true);
    this.camera.useAutoRotationBehavior = preview && !window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (this.camera.autoRotationBehavior) this.camera.autoRotationBehavior.idleRotationSpeed = 0.025;
    const ambient = new HemisphericLight('ambient', new Vector3(0, 1, 0), this.scene);
    ambient.intensity = 0.75;
    ambient.groundColor = Color3.FromHexString('#27352e');
    const sun = new DirectionalLight('sun', new Vector3(-0.6, -1.8, 0.8), this.scene);
    sun.position = new Vector3(10, 25, -15);
    sun.intensity = 2;
    sun.diffuse = Color3.FromHexString('#ffe3b3');
    const shadows = new ShadowGenerator(1024, sun);
    shadows.usePercentageCloserFiltering = true;
    shadows.darkness = 0.35;
    this.table();
    this.routes();
    for (const territory of map.territories) this.createTerritory(territory.id, shadows);
    let downX = 0, downY = 0;
    this.scene.onPointerObservable.add(info => {
      if (info.type === PointerEventTypes.POINTERDOWN) { downX = info.event.clientX; downY = info.event.clientY; }
      if (info.type !== PointerEventTypes.POINTERUP || Math.hypot(info.event.clientX - downX, info.event.clientY - downY) > 8) return;
      const id = info.pickInfo?.pickedMesh?.metadata?.territory as number | undefined;
      if (id !== undefined) onSelect(id);
    });
    this.resize = new ResizeObserver(() => { this.engine.resize(); this.fitCamera(); });
    this.resize.observe(canvas);
    this.update(null, null, []);
    this.engine.runRenderLoop(() => {
      this.elapsed += this.engine.getDeltaTime() / 1000;
      if (this.selected !== null) this.territories[this.selected].ring.scaling.setAll(1 + Math.sin(this.elapsed * 3) * 0.025);
      this.scene.render();
      canvas.dataset.ready = 'true';
    });
  }

  private surface(name: string, color: string, metallic = 0.1): PBRMaterial {
    const material = new PBRMaterial(name, this.scene);
    material.albedoColor = Color3.FromHexString(color);
    material.metallic = metallic;
    material.roughness = 0.75;
    return material;
  }

  private table() {
    const frame = MeshBuilder.CreateBox('wooden-frame', { width: 37.8, height: 0.7, depth: 25.4 }, this.scene);
    frame.position = new Vector3(1.4, -0.66, 0);
    frame.material = this.surface('frame-brass', '#826749', 0.6);
    frame.isPickable = false;
    const ocean = MeshBuilder.CreateBox('ocean', { width: 37, height: 0.18, depth: 24.6 }, this.scene);
    ocean.position = new Vector3(1.4, -0.23, 0);
    ocean.material = this.surface('sea', '#203e3b', 0.25);
    ocean.receiveShadows = true;
    ocean.isPickable = false;
    const gridColor = Color3.FromHexString('#395954');
    const lines: Vector3[][] = [];
    for (let x = -16; x <= 19; x += 2) lines.push([new Vector3(x, -0.12, -12), new Vector3(x, -0.12, 12)]);
    for (let z = -12; z <= 12; z += 2) lines.push([new Vector3(-17, -0.12, z), new Vector3(19, -0.12, z)]);
    const grid = MeshBuilder.CreateLineSystem('ocean-grid', { lines }, this.scene);
    grid.color = gridColor;
    grid.alpha = 0.3;
    grid.isPickable = false;
    const compass = MeshBuilder.CreateTorus('compass', { diameter: 3, thickness: 0.02, tessellation: 64 }, this.scene);
    compass.position = new Vector3(-12, -0.08, -8);
    compass.material = this.surface('compass-brass', '#a28a5c', 0.5);
    compass.isPickable = false;
    const points = [new Vector3(-12, -0.06, -6.4), new Vector3(-11.6, -0.06, -8), new Vector3(-12, -0.06, -9.6), new Vector3(-12.4, -0.06, -8), new Vector3(-12, -0.06, -6.4)];
    const arrow = MeshBuilder.CreateLines('compass-arrow', { points }, this.scene);
    arrow.color = Color3.FromHexString('#a28a5c');
    arrow.isPickable = false;
  }

  private routes() {
    for (const territory of map.territories) {
      for (const id of territory.neighbors.filter(n => n > territory.id)) {
        const target = map.territories[id];
        if (territory.continent === target.continent && Math.hypot(territory.x - target.x, territory.z - target.z) < 4) continue;
        if (Math.abs(territory.x - target.x) > 20) {
          for (const t of [territory, target]) this.route(new Vector3(t.x, 0.12, t.z), new Vector3(t.x < 0 ? -17 : 18.5, 0.12, t.z + 0.4));
        } else this.route(new Vector3(territory.x, 0.12, territory.z), new Vector3(target.x, 0.12, target.z));
      }
    }
  }

  private route(from: Vector3, to: Vector3) {
    const line = MeshBuilder.CreateDashedLines('sea-route', { points: [from, to], dashSize: 0.1, gapSize: 0.15, dashNb: 16 }, this.scene);
    line.color = Color3.FromHexString('#b49d6d');
    line.alpha = 0.6;
    line.isPickable = false;
  }

  private createTerritory(id: number, shadows: ShadowGenerator) {
    const territory = map.territories[id];
    const continent = map.continents.find(c => c.id === territory.continent)!;
    const mesh = new PolygonMeshBuilder(`land-${id}`, territory.shape.map(p => new Vector2(p[0], p[1])), this.scene, earcut).build(false, 0.28);
    mesh.position.y = 0.2;
    const material = this.surface(`land-color-${id}`, continent.color);
    mesh.material = material;
    mesh.metadata = { territory: id };
    mesh.receiveShadows = true;
    mesh.enableEdgesRendering();
    mesh.edgesWidth = 1.5;
    mesh.edgesColor = new Color4(0.12, 0.2, 0.17, 0.8);
    shadows.addShadowCaster(mesh);
    const unit = MeshBuilder.CreateCylinder(`army-${id}`, { diameterBottom: 0.55, diameterTop: 0.3, height: 0.7, tessellation: 8 }, this.scene);
    unit.position = new Vector3(territory.x - 0.3, 0.65, territory.z);
    unit.metadata = { territory: id };
    unit.material = this.surface(`army-color-${id}`, '#efe3c4', 0.5);
    shadows.addShadowCaster(unit);
    const ring = MeshBuilder.CreateTorus(`selection-${id}`, { diameter: 0.9, thickness: 0.04, tessellation: 32 }, this.scene);
    ring.position = new Vector3(territory.x, 0.24, territory.z);
    ring.material = this.surface(`ring-${id}`, '#ffdf86', 0.7);
    ring.isPickable = false;
    ring.setEnabled(false);
    const label = new DynamicTexture(`label-${id}`, { width: 256, height: 128 }, this.scene, false);
    label.hasAlpha = true;
    const tag = MeshBuilder.CreatePlane(`tag-${id}`, { width: 2.9, height: 1.45 }, this.scene);
    tag.position = new Vector3(territory.x, 1.5, territory.z - 0.22);
    tag.billboardMode = Mesh.BILLBOARDMODE_ALL;
    tag.metadata = { territory: id };
    const tagMaterial = new StandardMaterial(`tag-material-${id}`, this.scene);
    tagMaterial.diffuseTexture = label;
    tagMaterial.emissiveColor = Color3.White();
    tagMaterial.disableLighting = true;
    tagMaterial.backFaceCulling = false;
    tag.material = tagMaterial;
    this.territories.push({ mesh, material, label, tag, unit, ring, last: '' });
  }

  update(game: GameView | null, selected: number | null, reachable: number[]) {
    this.game = game;
    this.selected = selected;
    this.reachable = reachable;
    for (const territory of map.territories) {
      const art = this.territories[territory.id];
      const state = game?.territories[territory.id];
      const owner = state?.owner ?? -1;
      const color = owner >= 0 ? playerColors[owner] : map.continents.find(c => c.id === territory.continent)!.color;
      art.material.albedoColor = Color3.FromHexString(color).scale(selected === territory.id ? 1.25 : 0.85);
      art.material.emissiveColor = Color3.FromHexString(color).scale(reachable.includes(territory.id) ? 0.18 : 0.015);
      art.ring.setEnabled(territory.id === selected || reachable.includes(territory.id));
      const key = `${owner}:${state?.troops ?? 0}:${selected === territory.id}`;
      if (art.last === key) continue;
      art.last = key;
      const context = art.label.getContext() as CanvasRenderingContext2D;
      art.tag.setEnabled(game !== null);
      context.clearRect(0, 0, 256, 128);
      context.fillStyle = 'rgba(15, 25, 21, 0.88)';
      context.fillRect(3, 3, 250, 120);
      context.fillStyle = '#f6ecd8';
      context.font = '500 22px Arial';
      context.textAlign = 'center';
      const words = territory.name.replace('Northwest', 'NW').replace('Western', 'W.').replace('Eastern', 'E.').replace('Northern', 'N.').replace('Southern', 'S.').split(' ');
      if (words.join(' ').length > 16) {
        const split = Math.ceil(words.length / 2);
        context.fillText(words.slice(0, split).join(' '), 128, 92);
        context.fillText(words.slice(split).join(' '), 128, 115);
      } else context.fillText(words.join(' '), 128, 104);
      context.fillStyle = color;
      context.font = 'bold 52px Arial';
      context.fillText(state ? String(state.troops) : '◆', 128, 62);
      art.label.update();
      art.unit.setEnabled(!state || state.troops > 0);
      (art.unit.material as PBRMaterial).albedoColor = Color3.FromHexString(color);
      art.unit.scaling.y = state ? 0.8 + Math.min(1.6, Math.log2(state.troops + 1) * 0.2) : 1;
    }
  }

  private fitCamera() { this.camera.radius = Math.min(80, Math.max(45, 50 / Math.max(0.4, this.engine.getAspectRatio(this.camera)))); }
  resetCamera() { this.camera.alpha = -Math.PI / 2; this.camera.beta = 0.64; this.fitCamera(); this.camera.target = new Vector3(1.2, 0, 0); }
  dispose() { this.resize.disconnect(); this.scene.dispose(); this.engine.dispose(); }
}

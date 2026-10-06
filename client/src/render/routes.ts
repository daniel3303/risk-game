import { StandardMaterial } from '@babylonjs/core/Materials/standardMaterial';
import { Color3 } from '@babylonjs/core/Maths/math.color';
import { Vector3 } from '@babylonjs/core/Maths/math.vector';
import { Mesh } from '@babylonjs/core/Meshes/mesh';
import { MeshBuilder } from '@babylonjs/core/Meshes/meshBuilder';
import type { Scene } from '@babylonjs/core/scene';
import { map } from '../game/map';
import { seaHeight } from './terrain';

const connections = [[0,29],[2,13],[13,14],[13,16],[14,16],[16,17],[16,18],[19,21],[19,35],[18,20],[11,20],[24,25],[22,25],[31,32],[29,32],[37,38],[38,39],[38,40],[39,40],[39,41]];
const routeHeight = seaHeight + .12;
const cross = (a: number[], b: number[]) => a[0] * b[1] - a[1] * b[0];

function shoreline(id: number, target: Vector3) {
  const territory = map.territories[id];
  const ray = [target.x - territory.x, target.z - territory.z];
  let exit = 1;
  for (let i = 0; i < territory.shape.length; i++) {
    const a = territory.shape[i], b = territory.shape[(i + 1) % territory.shape.length];
    const edge = [b[0] - a[0], b[1] - a[1]], offset = [a[0] - territory.x, a[1] - territory.z];
    const denominator = cross(ray, edge);
    if (Math.abs(denominator) < 1e-8) continue;
    const t = cross(offset, edge) / denominator, u = cross(offset, ray) / denominator;
    if (t > 0 && t < exit && u >= 0 && u <= 1) exit = t;
  }
  const length = Math.hypot(...ray);
  return new Vector3(territory.x + ray[0] * exit + ray[0] / length * .2, routeHeight, territory.z + ray[1] * exit + ray[1] / length * .2);
}

function dottedRoute(scene: Scene, from: Vector3, to: Vector3, material: StandardMaterial) {
  const steps = Math.max(3, Math.ceil(Vector3.Distance(from, to) / .55));
  const point = (t: number) => { const p = Vector3.Lerp(from, to, t); p.z += Math.sin(t * Math.PI) * .22; return p; };
  const pieces = Array.from({ length: steps }, (_, i) => MeshBuilder.CreateTube('route-dash', {
    path: [point(i / steps), point((i + .62) / steps)], radius: .045, tessellation: 6, cap: Mesh.CAP_ALL,
  }, scene));
  const mesh = Mesh.MergeMeshes(pieces, true, true)!;
  mesh.material = material; mesh.isPickable = false;
}

function port(scene: Scene, point: Vector3, white: StandardMaterial, black: StandardMaterial) {
  const ring = MeshBuilder.CreateTorus('shore-port-rim', { diameter: .37, thickness: .1, tessellation: 12 }, scene);
  ring.position.copyFrom(point); ring.material = black; ring.isPickable = false;
  const marker = MeshBuilder.CreateSphere('shore-port', { diameter: .27, segments: 8 }, scene);
  marker.position.copyFrom(point); marker.position.y += .03; marker.material = white; marker.isPickable = false;
}

export function createSeaRoutes(scene: Scene) {
  const white = new StandardMaterial('sea-route-white', scene);
  white.disableLighting = true; white.emissiveColor = Color3.FromHexString('#e3f7fa');
  const black = new StandardMaterial('sea-route-black', scene);
  black.disableLighting = true; black.emissiveColor = Color3.FromHexString('#071116');
  for (const [from, to] of connections) {
    const a = map.territories[from], b = map.territories[to];
    const centerA = new Vector3(a.x, routeHeight, a.z), centerB = new Vector3(b.x, routeHeight, b.z);
    const wrapped = Math.abs(a.x - b.x) > 30;
    const destinationA = wrapped ? new Vector3(-24, routeHeight, a.z + .4) : centerB;
    const destinationB = wrapped ? new Vector3(24, routeHeight, b.z + .4) : centerA;
    dottedRoute(scene, centerA, destinationA, white);
    if (wrapped) dottedRoute(scene, centerB, destinationB, white);
    port(scene, shoreline(from, destinationA), white, black);
    port(scene, shoreline(to, destinationB), white, black);
  }
}

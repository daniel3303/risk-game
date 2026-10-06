import { Mesh } from '@babylonjs/core/Meshes/mesh';
import { MeshBuilder } from '@babylonjs/core/Meshes/meshBuilder';
import { StandardMaterial } from '@babylonjs/core/Materials/standardMaterial';
import { Color3 } from '@babylonjs/core/Maths/math.color';
import { VertexBuffer } from '@babylonjs/core/Buffers/buffer';
import type { Scene } from '@babylonjs/core/scene';

export function createInfantry(scene: Scene, id: number): Mesh {
  const uniform = new StandardMaterial(`uniform-${id}`, scene);
  uniform.diffuseColor = Color3.FromHexString('#e7ce79'); uniform.specularColor = new Color3(.35, .35, .35);
  const dark = new StandardMaterial(`equipment-${id}`, scene);
  dark.diffuseColor = Color3.FromHexString('#233043');
  const face = new StandardMaterial(`skin-${id}`, scene);
  face.diffuseColor = Color3.FromHexString('#efc69e');
  const parts: Mesh[] = [];
  const piece = (name: string, size: { width: number; height: number; depth: number }, x: number, y: number, z: number, material = uniform) => {
    const mesh = MeshBuilder.CreateBox(`${name}-${id}`, size, scene);
    mesh.position.set(x, y, z); mesh.material = material; parts.push(mesh); return mesh;
  };
  piece('coat', { width: .22, height: .34, depth: .16 }, 0, .46, 0);
  piece('left-boot', { width: .095, height: .25, depth: .13 }, -.065, .17, 0, dark);
  piece('right-boot', { width: .095, height: .25, depth: .13 }, .065, .17, 0, dark);
  piece('left-sleeve', { width: .07, height: .25, depth: .1 }, -.15, .46, 0).rotation.z = -.2;
  piece('right-sleeve', { width: .07, height: .25, depth: .1 }, .15, .46, 0).rotation.z = .2;
  const head = MeshBuilder.CreateSphere(`head-${id}`, { diameter: .17, segments: 6 }, scene);
  head.position.y = .72; head.material = face; parts.push(head);
  piece('bicorne', { width: .34, height: .1, depth: .13 }, 0, .81, 0, dark);
  piece('rifle', { width: .035, height: .52, depth: .035 }, .2, .48, 0, dark).rotation.z = -.18;
  for (const part of parts) {
    const color = (part.material as StandardMaterial).diffuseColor.toLinearSpace();
    const colors = Array.from({ length: part.getTotalVertices() }, () => [color.r, color.g, color.b, 1]).flat();
    part.setVerticesData(VertexBuffer.ColorKind, colors);
    part.material = uniform;
  }
  uniform.diffuseColor = Color3.White();
  const mesh = Mesh.MergeMeshes(parts, true, true)!;
  mesh.name = `infantry-${id}`; mesh.metadata = { territory: id };
  return mesh;
}

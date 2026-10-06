import { DynamicTexture } from '@babylonjs/core/Materials/Textures/dynamicTexture';
import { StandardMaterial } from '@babylonjs/core/Materials/standardMaterial';
import { Color3 } from '@babylonjs/core/Maths/math.color';
import { MeshBuilder } from '@babylonjs/core/Meshes/meshBuilder';
import type { Scene } from '@babylonjs/core/scene';

export function createOcean(scene: Scene) {
  const texture = new DynamicTexture('ocean-chart', { width: 2048, height: 1024 }, scene, false);
  const ctx = texture.getContext() as CanvasRenderingContext2D;
  const sea = ctx.createRadialGradient(1100, 440, 80, 1024, 512, 1280);
  sea.addColorStop(0, '#28b3d5'); sea.addColorStop(0.55, '#168bb7'); sea.addColorStop(1, '#06476d');
  ctx.fillStyle = sea; ctx.fillRect(0, 0, 2048, 1024);
  ctx.strokeStyle = 'rgba(171,229,246,.10)'; ctx.lineWidth = 1;
  for (let i = 0; i < 90; i++) {
    const x = (i * 397 + 19) % 2048, y = (i * 233 + 113) % 1024;
    ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo((x + 750 + i * 37) % 2048, 1024); ctx.stroke();
    if (i % 3 === 0) { ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(2048, (y + i * 11) % 1024); ctx.stroke(); }
  }
  ctx.strokeStyle = 'rgba(213,245,252,.07)';
  for (let x = 0; x < 2048; x += 128) { ctx.beginPath(); ctx.moveTo(x, 0); ctx.lineTo(x, 1024); ctx.stroke(); }
  for (let y = 0; y < 1024; y += 128) { ctx.beginPath(); ctx.moveTo(0, y); ctx.lineTo(2048, y); ctx.stroke(); }
  texture.update();
  const material = new StandardMaterial('ocean', scene);
  material.diffuseTexture = texture;
  material.emissiveColor = Color3.White();
  material.disableLighting = true;
  const plane = MeshBuilder.CreateGround('ocean', { width: 110, height: 60 }, scene);
  plane.position.y = -0.5; plane.material = material; plane.isPickable = false;
}

import { DynamicTexture } from '@babylonjs/core/Materials/Textures/dynamicTexture';
import type { Scene } from '@babylonjs/core/scene';

export const landHeight = 0.65;
export const seaHeight = -0.8;

export function createReliefTexture(scene: Scene) {
  const texture = new DynamicTexture('land-relief', { width: 256, height: 256 }, scene, false);
  const ctx = texture.getContext() as CanvasRenderingContext2D;
  const relief = ctx.createRadialGradient(115, 100, 12, 128, 128, 168);
  relief.addColorStop(0, '#ffffff');
  relief.addColorStop(.55, '#e0e3dc');
  relief.addColorStop(1, '#737d76');
  ctx.fillStyle = relief; ctx.fillRect(0, 0, 256, 256);
  for (let i = 0; i < 1800; i++) {
    ctx.fillStyle = i % 2 ? 'rgba(0,0,0,.025)' : 'rgba(255,255,255,.025)';
    ctx.fillRect((i * 127) % 256, (i * 53 + i * i) % 256, 1, 1);
  }
  texture.update();
  return texture;
}

import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: { proxy: { '/play': { target: 'http://localhost:5082', ws: true }, '/healthz': 'http://localhost:5082' } },
  build: { rollupOptions: { output: { manualChunks: id => id.includes('@babylonjs') ? 'babylon' : id.includes('@microsoft/signalr') ? 'signalr' : undefined } } },
  test: { include: ['src/**/*.test.ts'], restoreMocks: true },
});

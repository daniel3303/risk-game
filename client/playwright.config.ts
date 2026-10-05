import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  outputDir: '../artifacts/playwright',
  timeout: 45_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  workers: 1,
  reporter: [['list'], ['html', { outputFolder: '../artifacts/playwright-report', open: 'never' }]],
  use: { baseURL: process.env.RISK_BASE_URL ?? 'http://localhost:8092', trace: 'retain-on-failure', screenshot: 'only-on-failure', ...devices['Desktop Chrome'], launchOptions: { args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] } },
});

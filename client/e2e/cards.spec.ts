import { test, expect } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { build } from 'vite';
import react from '@vitejs/plugin-react';

test('landscape forced trade scrolls to the territory bonus and submits a valid set', async ({ page }) => {
  const root = fileURLToPath(new URL('../', import.meta.url));
  const entry = 'virtual:cards-fixture';
  const result = await build({
    configFile: false, root, logLevel: 'silent',
    define: { 'process.env.NODE_ENV': JSON.stringify('production') },
    plugins: [react(), {
      name: 'cards-fixture',
      enforce: 'pre',
      resolveId(id, importer) {
        if (id === entry || id.endsWith(`/${entry}`)) return `\0${entry}`;
        if (id === '../net/session' && importer?.endsWith('/ui/Cards.tsx')) return '\0cards-session';
      },
      load(id) {
        if (id === '\0cards-session') return 'export const session = { act: async command => { window.lastTrade = command; } };';
        if (id !== `\0${entry}`) return;
        return `
          import { createElement } from 'react';
          import { createRoot } from 'react-dom/client';
          import { Cards } from '/src/ui/Cards.tsx';
          import map from '/@fs/${fileURLToPath(new URL('../../content/classic.json', import.meta.url))}';
          const game = {
            phase: 'draft', currentPlayer: 0, trades: 0, forcedTrade: true,
            territories: map.territories.map(t => ({ id: t.id, owner: 0, troops: 3 })),
            hand: [0, 1, 2, 3, 4].map(id => ({ id, territory: id, symbol: 'infantry' }))
          };
          createRoot(document.getElementById('fixture')).render(createElement(Cards, {
            room: { options: { cards: 'fixed' } }, game, seat: 0, disabled: false, onClose() {}
          }));
        `;
      },
    }],
    build: { write: false, minify: false, lib: { entry, formats: ['iife'], name: 'CardsFixture' } },
  });
  const script = [result].flat().flatMap(item => 'output' in item ? item.output : []).find(item => item.type === 'chunk');
  if (!script) throw new Error('Cards fixture did not produce a browser bundle.');
  const styles = (await Promise.all([
    readFile(new URL('../src/styles.css', import.meta.url), 'utf8'),
    readFile(new URL('../src/campaign.css', import.meta.url), 'utf8'),
  ])).join('\n');
  await page.setViewportSize({ width: 844, height: 390 });
  await page.route('**/__cards-fixture.js', route => route.fulfill({ contentType: 'text/javascript', body: script.code }));
  await page.route('**/__cards-fixture', route => route.fulfill({
    contentType: 'text/html',
    headers: { 'Content-Security-Policy': "default-src 'none'; script-src 'self'; style-src 'unsafe-inline'" },
    body: `<style>${styles}</style><div class="app in-game"><header class="topbar">Risk</header><main class="game-layout"><div class="campaign-world"><div id="fixture"></div></div></main></div><script src="/__cards-fixture.js"></script>`,
  }));
  await page.goto('/__cards-fixture');
  await expect(page.getByText('You must trade a set before drafting.')).toBeVisible();
  for (let i = 0; i < 3; i++) await page.locator('.territory-card').nth(i).click();
  await page.getByLabel('Place the +2 territory bonus').selectOption('1');
  const trade = page.getByRole('button', { name: 'Trade for 4 troops', exact: true });
  await trade.scrollIntoViewIfNeeded();
  await expect(trade).toBeInViewport({ ratio: 1 });
  await trade.click();
  const command = await page.evaluate(() => (window as Window & { lastTrade?: unknown }).lastTrade);
  expect(command).toEqual({ kind: 'trade', cards: [0, 1, 2], bonusTerritory: 1 });
});

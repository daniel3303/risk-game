import { test, expect } from '@playwright/test';

test('host watches AI players, invites spectators, and resumes after reload', async ({ page, browser }) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto('/');
  await page.getByLabel('Your commander name').fill('AI observer');
  await page.getByLabel('Your role').selectOption('spectator');
  await page.getByRole('button', { name: 'Create AI room' }).click();
  await expect(page.getByTestId('spectator-status')).toContainText('AI observer (you) · Host');
  await expect(page.getByRole('button', { name: 'Begin World Domination' })).toBeDisabled();
  const code = await page.getByTestId('room-code').textContent();
  for (const difficulty of ['easy', 'hard']) {
    await page.getByLabel('AI difficulty').selectOption(difficulty);
    await page.getByRole('button', { name: 'Add AI player' }).click();
  }
  await expect(page.getByText('Hard AI 2', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Begin World Domination' }).click();
  await expect(page.getByTestId('turn-status')).toContainText('SPECTATING');
  await page.getByRole('button', { name: 'Battle history', exact: true }).click();
  await expect(page.locator('.activity ol li').first()).toBeVisible();
  const firstMove = await page.locator('.activity ol').textContent();
  await expect(page.locator('.activity ol')).not.toHaveText(firstMove!);
  await expect(page.getByRole('button', { name: 'Surrender', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Your cards', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: /Deploy .* troops/ })).toHaveCount(0);
  const guestContext = await browser.newContext();
  const guest = await guestContext.newPage();
  guest.on('pageerror', error => errors.push(error.message));
  await guest.goto(`/?room=${code}`);
  await guest.getByLabel('Your commander name').fill('Guest observer');
  await guest.getByRole('button', { name: 'Join the table' }).click();
  await expect(guest.getByTestId('turn-status')).toContainText('SPECTATING');
  await page.reload();
  await expect(page.getByTestId('turn-status')).toContainText('SPECTATING');
  await page.setViewportSize({ width: 375, height: 667 });
  await expect(page.getByRole('button', { name: 'Battle history', exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)).toBe(false);
  await page.getByLabel('Game menu', { exact: true }).click();
  await page.getByRole('button', { name: 'Leave table', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Create your table', exact: true })).toBeVisible();
  await expect(guest.getByTestId('turn-status')).toContainText('SPECTATING');
  expect(errors).toEqual([]);
  await guestContext.close();
});

test('board pans and zooms while mouse and keyboard cannot rotate or tilt it', async ({ page }) => {
  await page.goto('/');
  await page.getByLabel('Your commander name').fill('Fixed view');
  await page.getByRole('button', { name: 'Create your table' }).click();
  await page.getByRole('button', { name: 'Add AI player' }).click();
  await page.getByRole('button', { name: 'Begin World Domination' }).click();
  await expect(page.getByTestId('phase')).toHaveText('Draft');
  const canvas = page.getByTestId('world-board');
  await expect(canvas).toHaveAttribute('data-ready', 'true');
  const positions = () => page.locator('[data-map-label]').evaluateAll(elements => [0, 6, 22].map(id => {
    const el = elements[id] as HTMLElement;
    return [parseFloat(el.style.left), parseFloat(el.style.top)];
  }));
  const initial = await positions();
  const board = (await canvas.boundingBox())!;
  await page.mouse.move(board.x + 10, board.y + 200);
  await page.mouse.down();
  await page.mouse.move(board.x + 100, board.y + 250, { steps: 8 });
  await page.mouse.up();
  await expect.poll(positions).not.toEqual(initial);
  const assertOrientation = async () => {
    const moved = await positions();
    for (const id of [1, 2]) for (const axis of [0, 1]) {
      expect(Math.abs((moved[id][axis] - moved[0][axis]) - (initial[id][axis] - initial[0][axis]))).toBeLessThanOrEqual(2);
    }
  };
  await assertOrientation();
  await page.mouse.move(board.x + 10, board.y + 200);
  await page.mouse.down({ button: 'right' });
  await page.mouse.move(board.x + 100, board.y + 280, { steps: 8 });
  await page.mouse.up({ button: 'right' });
  await canvas.focus();
  await page.keyboard.press('ArrowLeft');
  await page.keyboard.press('ArrowUp');
  await assertOrientation();
  await page.getByRole('button', { name: 'Zoom in', exact: true }).click();
  await expect.poll(async () => {
    const moved = await positions();
    return Math.abs(moved[1][0] - moved[0][0]);
  }).toBeGreaterThan(Math.abs(initial[1][0] - initial[0][0]));
  await page.getByRole('button', { name: 'Reset camera', exact: true }).click();
  await expect.poll(positions).toEqual(initial);
});

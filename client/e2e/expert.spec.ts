import { test, expect } from '@playwright/test';

test('spectator selects Expert and watches its server-authoritative turns', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto('/');
  await page.getByLabel('Your commander name').fill('Expert observer');
  await page.getByLabel('Your role').selectOption('spectator');
  await page.getByRole('button', { name: 'Create AI room' }).click();
  await page.getByLabel('AI difficulty').selectOption('expert');
  await page.getByRole('button', { name: 'Add AI player' }).click();
  await expect(page.getByText('Expert AI 1', { exact: true })).toBeVisible();
  await expect(page.getByText('expert · Turn planner', { exact: true })).toBeVisible();
  await page.getByLabel('AI difficulty').selectOption('hard');
  await page.getByRole('button', { name: 'Add AI player' }).click();
  await page.getByRole('button', { name: 'Begin World Domination' }).click();
  await expect(page.getByTestId('turn-status')).toContainText('SPECTATING');
  await page.getByRole('button', { name: 'Battle history', exact: true }).click();
  await expect(page.locator('.activity ol')).toContainText('Expert AI 1');
  const first = await page.locator('.activity ol').textContent();
  await expect(page.locator('.activity ol')).not.toHaveText(first!);
  await expect(page.getByRole('button', { name: 'Your cards', exact: true })).toHaveCount(0);
  expect(errors).toEqual([]);
});

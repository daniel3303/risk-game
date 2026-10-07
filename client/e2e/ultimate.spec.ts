import { test, expect } from '@playwright/test';

test('spectator watches Ultimate play Master with server-authoritative turns', async ({ page }) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto('/');
  await page.getByLabel('Your commander name').fill('Ultimate observer');
  await page.getByLabel('Your role').selectOption('spectator');
  await page.getByRole('button', { name: 'Create AI room' }).click();
  await page.getByLabel('AI difficulty').selectOption('ultimate');
  await expect(page.getByText('Master guided by a value network learned from self-play. The strongest trained model.')).toBeVisible();
  await page.getByRole('button', { name: 'Add AI player' }).click();
  await expect(page.getByText('Ultimate AI 1', { exact: true })).toBeVisible();
  await expect(page.getByText('ultimate · Learned planner', { exact: true })).toBeVisible();
  await page.getByLabel('AI difficulty').selectOption('master');
  await page.getByRole('button', { name: 'Add AI player' }).click();
  await page.getByRole('button', { name: 'Begin World Domination' }).click();
  await expect(page.getByTestId('turn-status')).toContainText('SPECTATING');
  await page.getByRole('button', { name: 'Battle history', exact: true }).click();
  await expect(page.locator('.activity ol')).toContainText('Ultimate AI 1');
  const first = await page.locator('.activity ol').textContent();
  await expect(page.locator('.activity ol')).not.toHaveText(first!);
  expect(errors).toEqual([]);
});

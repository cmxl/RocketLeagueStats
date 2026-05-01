import { test, expect } from '@playwright/test';

test.describe('Landing page', () => {
  test('renders the chooser', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveTitle(/Rocket League Stats/i);
  });

  test('navigates to history', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('link', { name: /history/i }).click();
    await expect(page).toHaveURL(/\/history$/);
  });

  test('connection banner appears when hub is unreachable', async ({ page, context }) => {
    await context.route('**/hub/stats**', (route) => route.abort());
    await page.goto('/');
    await expect(page.getByText(/(Reconnecting to server|Disconnected)/i)).toBeVisible({ timeout: 10_000 });
  });
});

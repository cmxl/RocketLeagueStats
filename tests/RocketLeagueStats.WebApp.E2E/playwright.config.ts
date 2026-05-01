import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './specs',
  reporter: [['html', { open: 'never' }]],
  use: {
    baseURL: 'http://localhost:5000',
    trace: 'on-first-retry',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
});

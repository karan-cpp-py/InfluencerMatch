import { test, expect } from '@playwright/test';

async function seedAuth(page) {
  await page.addInitScript(() => {
    localStorage.setItem('token', 'fake-token');
    localStorage.setItem('role', 'Brand');
  });
}

async function stubPlatformConfig(page, overrides = {}) {
  const payload = {
    positioningLine: 'AI Creator Intelligence Platform for growth and sponsorship readiness.',
    phases: {
      creatorIntelligence: true,
      creatorGraph: true,
      creatorGraphPublicOptIn: true,
      brandActivation: false,
      brandPilotInviteOnly: true,
      ...overrides,
    },
    kpiGates: {
      activeCreatorsWeekly: 500,
      brandActivationCreatorThreshold: 1000,
      creatorThresholdReached: false,
    },
  };

  await page.route('**/api/platform/config', route => {
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(payload),
    });
  });
}

test('redirects brand user to waitlist when marketplace is disabled', async ({ page }) => {
  await seedAuth(page);
  await stubPlatformConfig(page, { creatorGraph: false, brandActivation: false });

  await page.goto('/marketplace');

  await expect(page).toHaveURL(/\/brand\/waitlist$/);
  await expect(page.getByRole('heading', { name: 'Brand Activation is Invite-Only' })).toBeVisible();
});

test('redirects brand user to waitlist when brand activation is disabled', async ({ page }) => {
  await seedAuth(page);
  await stubPlatformConfig(page, { creatorGraph: true, brandActivation: false });

  await page.goto('/brand');

  await expect(page).toHaveURL(/\/brand\/waitlist$/);
  await expect(page.getByRole('button', { name: 'Join Brand Waitlist' })).toBeVisible();
});

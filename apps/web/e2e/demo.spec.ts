import { expect, test } from '@playwright/test';

// Requires a seeded local API and web server; no mocked network requests.
test('demo volunteer accepts and declines an assignment, with persistence after refresh', async ({
  page,
}) => {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto('/');
  await expect(
    page.getByRole('heading', { name: 'Room for everyone.' }),
  ).toBeVisible();
  await page.getByRole('link', { name: /Step inside the demo/ }).click();
  await expect(
    page.getByText('This is a shared development demo.'),
  ).toBeVisible();
  await page.getByRole('button', { name: /Enter the demo/ }).click();
  await expect(
    page.getByRole('heading', { name: /Welcome back/ }),
  ).toBeVisible();
  await page.locator('.event-card').first().click();
  const own = page
    .locator('[data-testid="assignment"][data-current-user="true"]')
    .first();
  await expect(own).toBeVisible();
  // Normalize first, making the check repeatable without resetting the database.
  if ((await own.getAttribute('data-status')) !== 'Declined') {
    await own.getByRole('button', { name: /Decline/ }).click();
    await expect(own).toHaveAttribute('data-status', 'Declined');
  }
  await own.getByRole('button', { name: /Accept/ }).click();
  await expect(own).toHaveAttribute('data-status', 'Accepted');
  await expect(page.getByRole('status')).toContainText(
    'Your acceptance has been saved',
  );
  await page.reload();
  await expect(own).toHaveAttribute('data-status', 'Accepted');
  await own.getByRole('button', { name: /Decline/ }).click();
  await expect(own).toHaveAttribute('data-status', 'Declined');
  await page.reload();
  await expect(own).toHaveAttribute('data-status', 'Declined');
  await expect(own.getByRole('button', { name: /Accept/ })).toBeEnabled();
  await own.getByRole('button', { name: /Accept/ }).click();
  await expect(own).toHaveAttribute('data-status', 'Accepted');
  await page.screenshot({
    path: process.env.SCREENSHOT_DIR
      ? `${process.env.SCREENSHOT_DIR}/many-rooms-event.png`
      : 'test-results/many-rooms-event.png',
    fullPage: true,
  });
  expect(errors).toEqual([]);
});

test('responsive landing, keyboard entry, and community dashboard fit a mobile viewport', async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await expect(
    page.getByRole('heading', { name: 'Room for everyone.' }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({
    path: process.env.SCREENSHOT_DIR
      ? `${process.env.SCREENSHOT_DIR}/many-rooms-landing-mobile.png`
      : 'test-results/many-rooms-landing-mobile.png',
    fullPage: true,
  });
  await page.getByRole('link', { name: /Step inside the demo/ }).focus();
  await page.keyboard.press('Enter');
  await expect(
    page.getByRole('button', { name: /Enter the demo/ }),
  ).toBeVisible();
  await page.getByRole('button', { name: /Enter the demo/ }).focus();
  await page.keyboard.press('Enter');
  await expect(
    page.getByRole('heading', { name: /Welcome back/ }),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({
    path: process.env.SCREENSHOT_DIR
      ? `${process.env.SCREENSHOT_DIR}/many-rooms-dashboard-mobile.png`
      : 'test-results/many-rooms-dashboard-mobile.png',
    fullPage: true,
  });
  await page.locator('.event-card').first().click();
  await expect(
    page
      .locator('[data-testid="assignment"][data-current-user="true"]')
      .first(),
  ).toBeVisible();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
});

test('main screens meet automated WCAG AA checks and fit narrow phones', async ({
  page,
}) => {
  const AxeBuilder = (await import('@axe-core/playwright')).default;
  const check = async () => {
    const report = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();
    expect(
      report.violations.map((violation) => ({
        id: violation.id,
        failures: violation.nodes.map((node) => ({
          target: node.target,
          summary: node.failureSummary,
        })),
      })),
    ).toEqual([]);
    await page.locator('main').focus();
  };
  await page.goto('/');
  await expect(
    page.getByRole('heading', { name: 'Room for everyone.' }),
  ).toBeVisible();
  await check();
  await page.screenshot({
    path: process.env.SCREENSHOT_DIR
      ? `${process.env.SCREENSHOT_DIR}/many-rooms-landing.png`
      : 'test-results/many-rooms-landing.png',
    fullPage: true,
  });
  await page.getByRole('link', { name: /Step inside the demo/ }).click();
  await check();
  await page.getByRole('button', { name: /Enter the demo/ }).click();
  await expect(
    page.getByRole('heading', { name: /Welcome back/ }),
  ).toBeVisible();
  await check();
  await page.screenshot({
    path: process.env.SCREENSHOT_DIR
      ? `${process.env.SCREENSHOT_DIR}/many-rooms-dashboard.png`
      : 'test-results/many-rooms-dashboard.png',
    fullPage: true,
  });
  await page.locator('.event-card').first().click();
  await expect(
    page
      .locator('[data-testid="assignment"][data-current-user="true"]')
      .first(),
  ).toBeVisible();
  await check();
  await page.setViewportSize({ width: 320, height: 740 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
  await check();
  await page.goto('/');
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
});

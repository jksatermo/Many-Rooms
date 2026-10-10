import { expect, test } from '@playwright/test';
import type {
  Assignment,
  DemoContext,
  EventDetail,
  Unavailability,
} from '../src/types';

const apiBase = process.env.API_BASE_URL || 'http://localhost:5080';
const volunteerHeaders = { 'X-ManyRooms-Demo': 'true' };
const screenshot = (name: string) =>
  `${process.env.SCREENSHOT_DIR || 'test-results'}/${name}.png`;
const localInput = (instant: string) => {
  const date = new Date(instant);
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
};

// Both tests use the running PostgreSQL-backed API. No scheduling results are mocked.
test('unavailable time persists, creates a visible advisory conflict, and can be edited and removed', async ({
  page,
  request,
}) => {
  const contextResponse = await request.get(`${apiBase}/api/demo/context`);
  expect(contextResponse.ok()).toBe(true);
  const context: DemoContext = await contextResponse.json();
  const org = `${apiBase}/api/organizations/${context.organizationId}`;
  const dashboardResponse = await request.get(`${org}/dashboard`, {
    headers: volunteerHeaders,
  });
  const dashboard = (await dashboardResponse.json()) as {
    assignments: Assignment[];
  };
  let assignment = dashboard.assignments.find((item) => item.isCurrentUser)!;
  expect(assignment).toBeTruthy();
  if (assignment.status === 'Declined') {
    const response = await request.put(
      `${org}/assignments/${assignment.id}/response`,
      {
        headers: volunteerHeaders,
        data: { decision: 'Accepted', expectedVersion: assignment.version },
      },
    );
    expect(response.ok()).toBe(true);
    assignment = await response.json();
  }
  let created: Unavailability | undefined;
  try {
    await page.goto('/demo');
    await page.getByRole('button', { name: /Enter the demo/ }).click();
    await page
      .getByRole('navigation', { name: 'Community navigation' })
      .getByRole('link', { name: 'My availability' })
      .click();
    await expect(
      page.getByRole('heading', { name: 'Your unavailable time.' }),
    ).toBeVisible();
    const start = new Date(
      new Date(assignment.eventStartsAt).getTime() + 5 * 60_000,
    ).toISOString();
    const end = new Date(
      new Date(assignment.eventEndsAt).getTime() - 5 * 60_000,
    ).toISOString();
    await page
      .getByLabel('Start', { exact: true })
      .fill(await page.evaluate(localInput, start));
    await page
      .getByLabel('End', { exact: true })
      .fill(await page.evaluate(localInput, end));
    const note = 'Fictional plans for the scheduling walkthrough';
    await page.getByLabel(/Private note/).fill(note);
    const createResponse = page.waitForResponse(
      (response) =>
        response.url() === `${org}/availability` &&
        response.request().method() === 'POST',
    );
    await page.getByRole('button', { name: 'Save unavailable time' }).click();
    const response = await createResponse;
    expect(response.status()).toBe(201);
    created = await response.json();
    await expect(
      page.getByRole('status').filter({ hasText: 'has been saved' }),
    ).toBeVisible();
    await page.reload();
    const row = page
      .getByTestId('availability-entry')
      .filter({ hasText: note });
    await expect(row).toBeVisible();
    await row.getByRole('button', { name: 'Edit', exact: true }).click();
    await page.getByLabel(/Private note/).fill(`${note}, updated`);
    const updateResponse = page.waitForResponse(
      (reply) =>
        reply.url().endsWith(`/availability/${created!.id}`) &&
        reply.request().method() === 'PUT',
    );
    await page.getByRole('button', { name: 'Update unavailable time' }).click();
    const updated = await updateResponse;
    expect(updated.status()).toBe(200);
    created = await updated.json();
    await expect(
      page.getByText(`${note}, updated`, { exact: true }),
    ).toBeVisible();
    const AxeBuilder = (await import('@axe-core/playwright')).default;
    expect(
      (
        await new AxeBuilder({ page })
          .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
          .analyze()
      ).violations,
    ).toEqual([]);
    await page.locator('main').focus();
    await page.screenshot({
      path: screenshot('many-rooms-availability'),
      fullPage: true,
    });

    await page.goto(`/app/events/${assignment.eventId}`);
    const own = page
      .getByTestId('assignment')
      .filter({ hasText: assignment.roleName })
      .filter({ hasText: context.displayName })
      .first();
    await expect(own.getByTestId('schedule-conflict')).toContainText(
      /unavailable/i,
    );
    await expect(page.getByText(note, { exact: false })).toHaveCount(0);
    const savedEvent = await request.get(
      `${org}/events/${assignment.eventId}`,
      { headers: volunteerHeaders },
    );
    const details = (await savedEvent.json()) as EventDetail;
    expect(
      details.assignments
        .find((item) => item.id === assignment.id)
        ?.conflicts.some((conflict) => conflict.availabilityId === created!.id),
    ).toBe(true);
    await page.screenshot({
      path: screenshot('many-rooms-volunteer-conflicts'),
      fullPage: true,
    });
    await page.setViewportSize({ width: 320, height: 740 });
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true);
    expect(
      (
        await new AxeBuilder({ page })
          .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
          .analyze()
      ).violations,
    ).toEqual([]);

    await page.goto('/app/availability');
    await page
      .getByTestId('availability-entry')
      .filter({ hasText: `${note}, updated` })
      .getByRole('button', { name: 'Remove', exact: true })
      .click();
    await page.getByRole('button', { name: 'Confirm removal' }).click();
    await expect(
      page.getByRole('status').filter({ hasText: 'has been removed' }),
    ).toBeVisible();
    await page.reload();
    await expect(
      page.getByText(`${note}, updated`, { exact: true }),
    ).toHaveCount(0);
    const remaining = await request.get(`${org}/availability`, {
      headers: volunteerHeaders,
    });
    expect(
      ((await remaining.json()) as Unavailability[]).some(
        (item) => item.id === created!.id,
      ),
    ).toBe(false);
    created = undefined;
  } finally {
    // Remove only this test's range if an assertion failed partway through.
    if (created)
      await request.delete(
        `${org}/availability/${created.id}?expectedVersion=${created.version}`,
        { headers: volunteerHeaders },
      );
  }
});

test('demo leader reviews conflicts, previews an assignment, and keeps the volunteer boundary', async ({
  page,
  request,
}) => {
  const context: DemoContext = await (
    await request.get(`${apiBase}/api/demo/context`)
  ).json();
  const org = `${apiBase}/api/organizations/${context.organizationId}`;
  const events = (await (
    await request.get(`${org}/events`, { headers: volunteerHeaders })
  ).json()) as { id: string }[];
  const eventId = events[0].id;
  const denied = await request.get(`${org}/leader/events/${eventId}`, {
    headers: volunteerHeaders,
  });
  expect(denied.status()).toBe(403);
  await page.goto('/demo');
  await page.getByRole('button', { name: 'Enter demo leader view' }).click();
  await expect(
    page.getByText('Demo leader view', { exact: true }),
  ).toBeVisible();
  await page.locator('.event-card').first().click();
  await expect(
    page.getByRole('region', { name: 'Assignment overview' }),
  ).toBeVisible();
  await expect(page.getByTestId('leader-assignment').first()).toBeVisible();
  await expect(page.getByRole('button', { name: /^Accept/ })).toHaveCount(0);
  // An unsaved preview demonstrates advisory conflict detection without creating
  // another permanent assignment every time the browser suite is repeated.
  const select = page.getByLabel('Team member', { exact: true });
  await select.selectOption({
    label: (await select
      .locator('option')
      .filter({ hasText: context.displayName })
      .first()
      .textContent()) as string,
  });
  await page
    .getByLabel('Role', { exact: true })
    .fill('Preview: extra welcome support');
  await page.getByRole('button', { name: 'Preview assignment' }).click();
  await expect(
    page.getByRole('heading', { name: 'Schedule preview' }),
  ).toBeVisible();
  await expect(
    page.getByRole('button', { name: 'Save assignment' }),
  ).toBeEnabled();
  const AxeBuilder = (await import('@axe-core/playwright')).default;
  expect(
    (
      await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
        .analyze()
    ).violations,
  ).toEqual([]);
  await page.locator('main').focus();
  await page.screenshot({
    path: screenshot('many-rooms-leader-schedule'),
    fullPage: true,
  });
  await page.setViewportSize({ width: 320, height: 740 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBe(true);
  expect(
    (
      await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
        .analyze()
    ).violations,
  ).toEqual([]);
});

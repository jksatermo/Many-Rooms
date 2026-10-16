import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';
import { api, ApiError } from './api';
import { demoSession } from './session';
import { assignment, context, dashboard, event } from './test/fixtures';
import type { TeamMember, StaffingRequirement, EventTemplate } from './types';

vi.mock('./api', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api')>();
  return {
    ...original,
    api: Object.fromEntries(
      Object.keys(original.api).map((key) => [key, vi.fn()]),
    ),
  };
});
const leader = {
  ...context,
  role: 'Leader' as const,
  displayName: 'Jamie Chen',
};
const member: TeamMember = {
  id: assignment.teamMemberId,
  teamId: assignment.teamId,
  teamName: assignment.teamName,
  membershipId: 'membership-alex',
  volunteerId: 'alex',
  volunteerName: 'Alex Morgan',
  positionName: 'Greeter',
  version: 3,
};
const team = {
  id: member.teamId,
  name: member.teamName,
  description: 'A welcome for every neighbor.',
  memberCount: 1,
  upcomingAssignmentCount: 1,
};
const staffing: StaffingRequirement = {
  id: 'need-one',
  eventId: event.id,
  teamId: team.id,
  teamName: team.name,
  roleName: 'Greeter',
  desiredCount: 3,
  version: 4,
  assigned: 1,
  accepted: 0,
  pending: 1,
  declined: 1,
  conflicted: 1,
  openSpots: 2,
};
const template: EventTemplate = {
  id: 'template-one',
  title: 'Community supper',
  description: 'A shared meal.',
  location: 'Main hall',
  dayOfWeek: 3,
  localStartTime: '18:00',
  durationMinutes: 90,
  startsOn: '2099-06-01',
  timeZone: context.timeZone,
};
function show(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  );
}
const fill = (label: string, value: string) =>
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
beforeEach(() => {
  vi.resetAllMocks();
  demoSession.enter('leader');
  vi.mocked(api.context).mockResolvedValue(leader);
  vi.mocked(api.event).mockResolvedValue(event);
  vi.mocked(api.leaderEvent).mockResolvedValue(event);
  vi.mocked(api.dashboard).mockResolvedValue(dashboard);
  vi.mocked(api.teamMembers).mockResolvedValue([member]);
  vi.mocked(api.teams).mockResolvedValue([team]);
  vi.mocked(api.team).mockResolvedValue({
    ...team,
    members: [member],
    upcomingAssignments: [assignment],
  });
  vi.mocked(api.organizationMembers).mockResolvedValue([
    {
      id: member.membershipId,
      volunteerId: member.volunteerId,
      volunteerName: member.volunteerName,
    },
    {
      id: 'membership-robin',
      volunteerId: 'robin',
      volunteerName: 'Robin Lee',
    },
  ]);
  vi.mocked(api.staffing).mockResolvedValue([staffing]);
  vi.mocked(api.eventHistory).mockResolvedValue([]);
  vi.mocked(api.conflicts).mockResolvedValue([
    { ...assignment, status: 'Declined' },
  ]);
  vi.mocked(api.replacementCandidates).mockResolvedValue([
    {
      teamMemberId: 'replacement',
      membershipId: 'membership-robin',
      volunteerId: 'robin',
      volunteerName: 'Robin Lee',
      positionName: 'Greeter',
      roleMatch: true,
      upcomingAssignmentCount: 2,
    },
  ]);
  vi.mocked(api.eventTemplates).mockResolvedValue([template]);
});

describe('leader team and staffing tools', () => {
  it('keeps volunteer sessions out of the leader pages and their data requests', async () => {
    demoSession.enter();
    vi.mocked(api.context).mockResolvedValue(context);
    show('/app/leader/teams');
    await screen.findByRole('heading', {
      name: 'This is the demo leader view.',
    });
    expect(api.teams).not.toHaveBeenCalled();
    expect(
      screen.queryByRole('link', { name: 'Needs attention' }),
    ).not.toBeInTheDocument();
  });
  it('adds an existing organization member and edits an existing position with its version', async () => {
    vi.mocked(api.addTeamMember).mockResolvedValue({
      ...member,
      id: 'new',
      membershipId: 'membership-robin',
      volunteerName: 'Robin Lee',
    });
    vi.mocked(api.updateTeamMember).mockResolvedValue({
      ...member,
      positionName: 'Host',
      version: 4,
    });
    show(`/app/leader/teams/${team.id}`);
    await userEvent.selectOptions(
      await screen.findByLabelText('Organization member'),
      'membership-robin',
    );
    await userEvent.type(
      screen.getByLabelText('Position (optional)'),
      'Welcome host',
    );
    await userEvent.click(screen.getByRole('button', { name: 'Add member' }));
    await screen.findByText('Team member added.');
    expect(api.addTeamMember).toHaveBeenCalledWith(
      context.organizationId,
      team.id,
      'membership-robin',
      'Welcome host',
    );
    await userEvent.click(
      await screen.findByRole('button', { name: 'Edit position' }),
    );
    await userEvent.clear(screen.getByLabelText('Position for Alex Morgan'));
    await userEvent.type(
      screen.getByLabelText('Position for Alex Morgan'),
      'Host',
    );
    await userEvent.click(
      screen.getByRole('button', { name: 'Save position' }),
    );
    await screen.findByText('Position updated.');
    expect(api.updateTeamMember).toHaveBeenCalledWith(
      context.organizationId,
      member,
      'Host',
    );
  });
  it('shows the server removal guard without removing a member from the screen', async () => {
    vi.mocked(api.removeTeamMember).mockRejectedValue(
      new ApiError(
        409,
        'Resolve future assignments before removing this member.',
      ),
    );
    show(`/app/leader/teams/${team.id}`);
    await userEvent.click(
      await screen.findByRole('button', { name: 'Remove member' }),
    );
    expect(api.removeTeamMember).not.toHaveBeenCalled();
    await userEvent.click(
      screen.getByRole('button', { name: 'Confirm removal' }),
    );
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Resolve future assignments',
    );
    expect(
      screen.getByRole('heading', { name: 'Alex Morgan' }),
    ).toBeInTheDocument();
    expect(api.removeTeamMember).toHaveBeenCalledWith(
      context.organizationId,
      member,
    );
  });
  it('shows server staffing counts and saves explicit team/role requirements', async () => {
    vi.mocked(api.createStaffing).mockResolvedValue({
      ...staffing,
      id: 'second',
    });
    show(`/app/leader/events/${event.id}`);
    const card = await screen.findByTestId('staffing-requirement');
    expect(card).toHaveTextContent('1 / 3 assigned · 2 open spots');
    expect(card).toHaveTextContent('1 in conflict');
    await userEvent.selectOptions(
      screen.getByLabelText('Staffing team'),
      team.id,
    );
    await userEvent.type(
      screen.getByLabelText('Staffing role (optional)'),
      'Host',
    );
    fill('Volunteers needed', '2');
    await userEvent.click(
      screen.getByRole('button', { name: 'Save staffing need' }),
    );
    await screen.findByText('Staffing requirement saved.');
    expect(api.createStaffing).toHaveBeenCalledWith(
      context.organizationId,
      event.id,
      { teamId: team.id, roleName: 'Host', desiredCount: 2 },
    );
  });
  it('updates a staffing target with its current version and removes only after confirmation', async () => {
    vi.mocked(api.updateStaffing).mockResolvedValue({
      ...staffing,
      desiredCount: 4,
      version: 5,
    });
    vi.mocked(api.removeStaffing).mockResolvedValue();
    show(`/app/leader/events/${event.id}`);
    await userEvent.click(
      await screen.findByRole('button', { name: 'Edit need' }),
    );
    expect(screen.getByLabelText('Staffing team')).toBeDisabled();
    fill('Volunteers needed', '4');
    await userEvent.click(
      screen.getByRole('button', { name: 'Update staffing need' }),
    );
    await screen.findByText('Staffing requirement saved.');
    expect(api.updateStaffing).toHaveBeenCalledWith(
      context.organizationId,
      staffing,
      { teamId: team.id, roleName: 'Greeter', desiredCount: 4 },
    );
    await userEvent.click(
      await screen.findByRole('button', { name: 'Remove need' }),
    );
    expect(api.removeStaffing).not.toHaveBeenCalled();
    await userEvent.click(
      screen.getByRole('button', { name: 'Confirm removal' }),
    );
    await screen.findByText(
      'Staffing requirement removed. Existing assignments are unchanged.',
    );
  });
});

describe('assignment maintenance and replacement', () => {
  it('previews a suggested replacement and persists through the existing leader assignment API', async () => {
    vi.mocked(api.previewAssignmentEdit).mockResolvedValue({ conflicts: [] });
    vi.mocked(api.updateAssignment).mockResolvedValue({
      ...assignment,
      volunteerName: 'Robin Lee',
      teamMemberId: 'replacement',
      version: 2,
    });
    show('/app/leader/conflicts');
    await userEvent.click(
      await screen.findByRole('button', { name: 'Find a replacement' }),
    );
    expect(
      await screen.findByText(/Declined — replacement needed/),
    ).toBeInTheDocument();
    await userEvent.selectOptions(
      await screen.findByLabelText('Suggested volunteer'),
      'replacement',
    );
    expect(
      screen.getByRole('option', {
        name: /Robin Lee · 2 upcoming · position match/,
      }),
    ).toBeInTheDocument();
    await userEvent.click(
      screen.getByRole('button', { name: 'Preview change' }),
    );
    await screen.findByRole('heading', { name: 'Change preview' });
    expect(api.updateAssignment).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Save change' }));
    await screen.findByText(/Assignment updated for Robin Lee/);
    expect(api.updateAssignment).toHaveBeenCalledWith(
      context.organizationId,
      { ...assignment, status: 'Declined' },
      { teamMemberId: 'replacement', roleName: 'Greeter' },
    );
  });
  it('invalidates a preview after a role change and stops retrying a stale update', async () => {
    vi.mocked(api.previewAssignmentEdit).mockResolvedValue({ conflicts: [] });
    vi.mocked(api.updateAssignment).mockRejectedValue(
      new ApiError(409, 'The assignment changed.'),
    );
    show(`/app/leader/events/${event.id}`);
    await userEvent.click(
      (
        await screen.findAllByRole('button', {
          name: 'Edit or cancel assignment',
        })
      )[0],
    );
    await userEvent.click(
      screen.getByRole('button', { name: 'Preview change' }),
    );
    await screen.findByRole('button', { name: 'Save change' });
    await userEvent.type(screen.getByLabelText('Assignment role'), ' lead');
    expect(
      screen.queryByRole('button', { name: 'Save change' }),
    ).not.toBeInTheDocument();
    await userEvent.click(
      screen.getByRole('button', { name: 'Preview change' }),
    );
    await userEvent.click(
      await screen.findByRole('button', { name: 'Save change' }),
    );
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Close this editor and refresh',
    );
    expect(
      screen.getByRole('button', { name: 'Preview change' }),
    ).toBeDisabled();
  });
  it('requires confirmation before cancelling and retains a visible cancelled record', async () => {
    vi.mocked(api.cancelAssignment).mockResolvedValue({
      ...assignment,
      isCancelled: true,
      version: 2,
    });
    vi.mocked(api.leaderEvent)
      .mockResolvedValueOnce(event)
      .mockResolvedValue({
        ...event,
        assignments: [{ ...assignment, isCancelled: true, version: 2 }],
      });
    show(`/app/leader/events/${event.id}`);
    await userEvent.click(
      (
        await screen.findAllByRole('button', {
          name: 'Edit or cancel assignment',
        })
      )[0],
    );
    await userEvent.click(
      screen.getByRole('button', { name: 'Cancel assignment' }),
    );
    expect(api.cancelAssignment).not.toHaveBeenCalled();
    await userEvent.click(
      screen.getByRole('button', { name: 'Confirm cancellation' }),
    );
    await screen.findByText('Assignment cancelled. History is preserved.');
    expect(
      await screen.findByText('Cancelled', { exact: true }),
    ).toBeInTheDocument();
    expect(api.cancelAssignment).toHaveBeenCalledWith(
      context.organizationId,
      assignment,
    );
  });
  it('does not offer volunteer response controls for a cancelled assignment', async () => {
    demoSession.enter();
    vi.mocked(api.context).mockResolvedValue(context);
    vi.mocked(api.event).mockResolvedValue({
      ...event,
      assignments: [{ ...assignment, isCancelled: true }],
    });
    show(`/app/events/${event.id}`);
    await screen.findByText(
      'This assignment was cancelled. No response is needed.',
    );
    expect(
      screen.queryByRole('button', { name: /^Accept/ }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Decline' }),
    ).not.toBeInTheDocument();
  });
  it('shows imported history without inventing an actor', async () => {
    vi.mocked(api.eventHistory).mockResolvedValue([
      {
        id: 'history',
        assignmentId: assignment.id,
        action: 'Imported',
        occurredAt: '2026-10-14T12:00:00Z',
        actorName: null,
        previousVolunteerName: null,
        volunteerName: 'Alex Morgan',
        previousRoleName: null,
        roleName: 'Greeter',
        assignmentVersion: 1,
      },
    ]);
    show(`/app/leader/events/${event.id}`);
    await userEvent.click(
      await screen.findByRole('button', { name: 'View assignment history' }),
    );
    await screen.findByText(/Existing data ·/);
    expect(screen.getByText('Imported · Alex Morgan')).toBeInTheDocument();
  });
});

describe('one-time and weekly event planning', () => {
  it('creates a one-time event with explicit UTC instants and opens its schedule', async () => {
    vi.mocked(api.createEvent).mockResolvedValue(event);
    show('/app/leader/planning');
    await screen.findByRole('heading', { name: 'Plan an event.' });
    fill('Event title', 'A neighborhood supper');
    fill('Location', 'Community hall');
    fill('Event start', '2099-06-21T18:00');
    fill('Event end', '2099-06-21T19:30');
    await userEvent.click(screen.getByRole('button', { name: 'Create event' }));
    await screen.findByRole('heading', { name: event.title });
    expect(api.createEvent).toHaveBeenCalledWith(context.organizationId, {
      title: 'A neighborhood supper',
      description: '',
      location: 'Community hall',
      startsAt: new Date('2099-06-21T18:00').toISOString(),
      endsAt: new Date('2099-06-21T19:30').toISOString(),
    });
  });
  it('submits weekly local time in the organization zone, then shows bounded generation outcomes', async () => {
    vi.mocked(api.createEventTemplate).mockResolvedValue(template);
    vi.mocked(api.generateEvents).mockResolvedValue({
      createdEvents: [],
      existingCount: 4,
      skippedDates: ['2099-03-08'],
    });
    show('/app/leader/planning');
    await userEvent.click(
      await screen.findByRole('button', { name: 'Weekly template' }),
    );
    fill('Event title', template.title);
    fill('Location', template.location);
    await userEvent.selectOptions(screen.getByLabelText('Day of week'), '3');
    fill('Local start time', '18:00');
    fill('Starting on or after', template.startsOn);
    await userEvent.click(
      screen.getByRole('button', { name: 'Save weekly template' }),
    );
    await screen.findByText(/Weekly template saved/);
    expect(api.createEventTemplate).toHaveBeenCalledWith(
      context.organizationId,
      {
        title: template.title,
        description: '',
        location: template.location,
        dayOfWeek: 3,
        localStartTime: '18:00',
        durationMinutes: 90,
        startsOn: template.startsOn,
      },
    );
    fill('Generate through date', '2099-06-29');
    await userEvent.click(
      screen.getByRole('button', { name: 'Generate events' }),
    );
    await screen.findByText('0 events created · 4 already exist.');
    expect(
      screen.getByText(/Skipped local dates: 2099-03-08/),
    ).toBeInTheDocument();
    expect(api.generateEvents).toHaveBeenCalledWith(
      context.organizationId,
      template.id,
      '2099-06-29',
    );
  });
  it('keeps an empty replacement result explicit without allowing an invalid preview', async () => {
    vi.mocked(api.replacementCandidates).mockResolvedValue([]);
    show('/app/leader/conflicts');
    await userEvent.click(
      await screen.findByRole('button', { name: 'Find a replacement' }),
    );
    await screen.findByText(/No available replacement was found/);
    expect(
      screen.getByRole('button', { name: 'Preview change' }),
    ).toBeDisabled();
  });
  it('surfaces conflict-list errors and can retry to an empty state', async () => {
    vi.mocked(api.conflicts)
      .mockRejectedValueOnce(new Error('Schedule is temporarily unavailable.'))
      .mockResolvedValue([]);
    show('/app/leader/conflicts');
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Schedule is temporarily unavailable.',
    );
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    await screen.findByRole('heading', { name: 'Room to breathe.' });
    await waitFor(() => expect(api.conflicts).toHaveBeenCalledTimes(2));
    expect(
      within(
        screen.getByRole('navigation', { name: 'Community navigation' }),
      ).getByRole('link', { name: 'Needs attention' }),
    ).toHaveAttribute('aria-current', 'page');
  });
});

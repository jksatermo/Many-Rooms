import {
  act,
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
import type { ScheduleConflict, Unavailability } from './types';

vi.mock('./api', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api')>();
  return {
    ...original,
    api: Object.fromEntries(
      Object.keys(original.api).map((key) => [key, vi.fn()]),
    ),
  };
});

const conflict: ScheduleConflict = {
  type: 'OverlappingAssignment',
  message: 'Overlaps your assignment at Neighborhood meal.',
  startsAt: '2099-06-21T15:30:00Z',
  endsAt: '2099-06-21T16:00:00Z',
  relatedEventId: 'event-two',
  relatedEventTitle: 'Neighborhood meal',
  relatedAssignmentId: 'assignment-two',
  availabilityId: null,
};
const unavailableConflict: ScheduleConflict = {
  ...conflict,
  type: 'Unavailable',
  message: 'Overlaps unavailable time.',
  relatedEventId: null,
  relatedEventTitle: null,
  relatedAssignmentId: null,
  availabilityId: 'availability-one',
};
const unavailable: Unavailability = {
  id: 'availability-one',
  startsAt: '2099-06-21T15:00:00Z',
  endsAt: '2099-06-21T16:00:00Z',
  note: 'Fictional family plans',
  version: 3,
};
const leaderContext = {
  ...context,
  userId: 'jamie',
  displayName: 'Jamie Chen',
  role: 'Leader' as const,
};
const member = {
  id: 'team-member-one',
  teamId: 'team-one',
  teamName: 'Welcome team',
  membershipId: 'membership-one',
  volunteerId: 'alex',
  volunteerName: 'Alex Morgan',
};
const renderApp = (path: string) =>
  render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  );
const fillTime = (label: string, value: string) =>
  fireEvent.change(screen.getByLabelText(label), { target: { value } });

beforeEach(() => {
  vi.resetAllMocks();
  demoSession.enter();
  vi.mocked(api.context).mockResolvedValue(context);
  vi.mocked(api.dashboard).mockResolvedValue(dashboard);
  vi.mocked(api.events).mockResolvedValue(dashboard.events);
  vi.mocked(api.event).mockResolvedValue(event);
  vi.mocked(api.availability).mockResolvedValue([unavailable]);
  vi.mocked(api.leaderEvent).mockResolvedValue({
    ...event,
    assignments: [
      { ...assignment, conflicts: [conflict, unavailableConflict] },
      event.assignments[1],
    ],
  });
  vi.mocked(api.teamMembers).mockResolvedValue([member]);
});

describe('server-provided conflicts', () => {
  it('does not let an older background read overwrite a newly saved response', async () => {
    let finishRead!: (value: typeof event) => void;
    vi.mocked(api.event)
      .mockResolvedValueOnce(event)
      .mockReturnValueOnce(
        new Promise((resolve) => {
          finishRead = resolve;
        }),
      );
    vi.mocked(api.respond).mockResolvedValue({
      ...assignment,
      version: 2,
      status: 'Accepted',
    });
    renderApp('/app/events/event-one');
    await screen.findByRole('heading', { name: event.title });
    fireEvent(window, new Event('focus'));
    await userEvent.click(screen.getByRole('button', { name: /Accept/ }));
    await waitFor(() =>
      expect(screen.getAllByTestId('assignment')[0]).toHaveAttribute(
        'data-status',
        'Accepted',
      ),
    );
    await act(async () => finishRead(event));
    expect(screen.getAllByTestId('assignment')[0]).toHaveAttribute(
      'data-status',
      'Accepted',
    );
  });
  it('refreshes server changes when returning to the tab without interrupting a response in flight', async () => {
    vi.mocked(api.event)
      .mockResolvedValueOnce(event)
      .mockResolvedValue({
        ...event,
        assignments: [{ ...assignment, conflicts: [conflict] }],
      });
    let finish!: (value: typeof assignment) => void;
    vi.mocked(api.respond).mockReturnValue(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    renderApp('/app/events/event-one');
    await screen.findByRole('heading', { name: event.title });
    fireEvent(window, new Event('focus'));
    expect(await screen.findByText(conflict.message)).toBeInTheDocument();
    expect(api.event).toHaveBeenCalledTimes(2);
    await userEvent.click(screen.getByRole('button', { name: /Accept/ }));
    fireEvent(window, new Event('focus'));
    expect(api.event).toHaveBeenCalledTimes(2);
    await act(async () =>
      finish({
        ...assignment,
        status: 'Accepted',
        version: 2,
        conflicts: [conflict],
      }),
    );
    expect(screen.getByTestId('assignment')).toHaveAttribute(
      'data-status',
      'Accepted',
    );
  });
  it('shows both conflict explanations without preventing a volunteer response', async () => {
    vi.mocked(api.event).mockResolvedValue({
      ...event,
      assignments: [
        { ...assignment, conflicts: [conflict, unavailableConflict] },
      ],
    });
    renderApp('/app/events/event-one');
    expect(await screen.findByText(conflict.message)).toBeInTheDocument();
    expect(screen.getByText(unavailableConflict.message)).toBeInTheDocument();
    expect(
      screen.getByText(/assignment hasn’t been changed/),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Accept/ })).toBeEnabled();
    expect(screen.getAllByText(/10:30 AM CDT/)).toHaveLength(2);
  });
  it('keeps accepted assignments with conflicts visible on the overview', async () => {
    vi.mocked(api.dashboard).mockResolvedValue({
      ...dashboard,
      assignments: [
        { ...assignment, status: 'Accepted', conflicts: [conflict] },
      ],
    });
    renderApp('/app');
    expect(
      await screen.findByRole('heading', { name: 'A few things overlap.' }),
    ).toBeInTheDocument();
    expect(screen.getByText(conflict.message)).toBeInTheDocument();
    expect(screen.getByText('Accepted')).toBeInTheDocument();
  });
  it('removes resolved conflicts using the server response after a decline', async () => {
    vi.mocked(api.event).mockResolvedValue({
      ...event,
      assignments: [{ ...assignment, conflicts: [conflict] }],
    });
    vi.mocked(api.respond).mockResolvedValue({
      ...assignment,
      status: 'Declined',
      version: 2,
      conflicts: [],
    });
    renderApp('/app/events/event-one');
    await screen.findByText(conflict.message);
    await userEvent.click(screen.getByRole('button', { name: /Decline/ }));
    await waitFor(() =>
      expect(screen.queryByTestId('schedule-conflict')).not.toBeInTheDocument(),
    );
    expect(screen.getByTestId('assignment')).toHaveAttribute(
      'data-status',
      'Declined',
    );
  });
});

describe('my unavailable time', () => {
  it('loads saved ranges and explains the device and community time zones', async () => {
    renderApp('/app/availability');
    expect(await screen.findByText(unavailable.note!)).toBeInTheDocument();
    expect(
      screen.getByText(/Enter times in your device’s zone/),
    ).toHaveTextContent('America/Chicago');
    expect(
      screen.getByText(/Leaders see the time conflict, not the note/),
    ).toBeInTheDocument();
  });
  it('saves a UTC range and trimmed optional note then reloads persisted data', async () => {
    vi.mocked(api.createAvailability).mockResolvedValue(unavailable);
    renderApp('/app/availability');
    await screen.findByText(unavailable.note!);
    fillTime('Start', '2099-06-21T10:00');
    fillTime('End', '2099-06-21T11:00');
    await userEvent.type(
      screen.getByLabelText(/Private note/),
      '  Fictional plans  ',
    );
    await userEvent.click(
      screen.getByRole('button', { name: 'Save unavailable time' }),
    );
    expect(api.createAvailability).toHaveBeenCalledWith(
      context.organizationId,
      {
        startsAt: '2099-06-21T15:00:00.000Z',
        endsAt: '2099-06-21T16:00:00.000Z',
        note: 'Fictional plans',
      },
    );
    await waitFor(() => expect(api.availability).toHaveBeenCalledTimes(2));
    expect(screen.getByRole('status')).toHaveTextContent('has been saved');
  });
  it('rejects nonpositive ranges before making a request', async () => {
    renderApp('/app/availability');
    await screen.findByText(unavailable.note!);
    fillTime('Start', '2099-06-21T10:00');
    fillTime('End', '2099-06-21T10:00');
    await userEvent.click(
      screen.getByRole('button', { name: 'Save unavailable time' }),
    );
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'End must be later than start',
    );
    expect(api.createAvailability).not.toHaveBeenCalled();
  });
  it('edits using the current version and preserves exact instants for note-only changes', async () => {
    const precise = { ...unavailable, startsAt: '2099-06-21T15:00:31Z' };
    vi.mocked(api.availability).mockResolvedValue([precise]);
    vi.mocked(api.updateAvailability).mockResolvedValue({
      ...precise,
      note: null,
      version: 4,
    });
    renderApp('/app/availability');
    await screen.findByText(unavailable.note!);
    await userEvent.click(screen.getByRole('button', { name: 'Edit' }));
    expect(screen.getByLabelText('Start')).toHaveFocus();
    await userEvent.clear(screen.getByLabelText(/Private note/));
    await userEvent.click(
      screen.getByRole('button', { name: 'Update unavailable time' }),
    );
    expect(api.updateAvailability).toHaveBeenCalledWith(
      context.organizationId,
      precise,
      { startsAt: precise.startsAt, endsAt: precise.endsAt, note: null },
    );
  });
  it('refreshes stale edits and requires a new deliberate edit', async () => {
    vi.mocked(api.updateAvailability).mockRejectedValue(
      new ApiError(409, 'Changed elsewhere.'),
    );
    renderApp('/app/availability');
    await screen.findByText(unavailable.note!);
    await userEvent.click(screen.getByRole('button', { name: 'Edit' }));
    await userEvent.click(
      screen.getByRole('button', { name: 'Update unavailable time' }),
    );
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Review it before trying again',
    );
    await waitFor(() => expect(api.availability).toHaveBeenCalledTimes(2));
    expect(api.updateAvailability).toHaveBeenCalledTimes(1);
    expect(
      screen.queryByRole('button', { name: 'Update unavailable time' }),
    ).not.toBeInTheDocument();
  });
  it('confirms removal, disables repeat submissions, and reloads after deletion', async () => {
    let finish!: () => void;
    vi.mocked(api.removeAvailability).mockReturnValue(
      new Promise<void>((resolve) => {
        finish = resolve;
      }),
    );
    renderApp('/app/availability');
    await screen.findByText(unavailable.note!);
    await userEvent.click(screen.getByRole('button', { name: 'Remove' }));
    expect(api.removeAvailability).not.toHaveBeenCalled();
    await userEvent.click(
      screen.getByRole('button', { name: 'Confirm removal' }),
    );
    expect(
      screen.getByRole('button', { name: 'Confirm removal' }),
    ).toBeDisabled();
    expect(api.removeAvailability).toHaveBeenCalledWith(
      context.organizationId,
      unavailable,
    );
    vi.mocked(api.availability).mockResolvedValue([]);
    await act(async () => finish());
    expect(
      await screen.findByRole('heading', { name: 'No unavailable time yet.' }),
    ).toBeInTheDocument();
  });
  it('keeps input after a failed save and offers retry for a failed list', async () => {
    vi.mocked(api.availability)
      .mockRejectedValueOnce(new ApiError(503, 'Please try again later.'))
      .mockResolvedValueOnce([]);
    vi.mocked(api.createAvailability).mockRejectedValue(
      new ApiError(503, 'Save unavailable.'),
    );
    renderApp('/app/availability');
    await userEvent.click(
      await screen.findByRole('button', { name: 'Try again' }),
    );
    await screen.findByRole('heading', { name: 'No unavailable time yet.' });
    fillTime('Start', '2099-06-21T10:00');
    fillTime('End', '2099-06-21T11:00');
    await userEvent.click(
      screen.getByRole('button', { name: 'Save unavailable time' }),
    );
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Save unavailable.',
    );
    expect(screen.getByLabelText('Start')).toHaveValue('2099-06-21T10:00');
    expect(
      screen.getByRole('button', { name: 'Save unavailable time' }),
    ).toBeEnabled();
  });
});

describe('explicit demo leader schedule', () => {
  function enterLeader() {
    demoSession.enter('leader');
    vi.mocked(api.context).mockResolvedValue(leaderContext);
  }
  it('opens the leader persona only after its explicitly labeled entry action', async () => {
    demoSession.leave();
    vi.mocked(api.context).mockResolvedValue(leaderContext);
    renderApp('/demo');
    await userEvent.click(
      screen.getByRole('button', { name: 'Enter demo leader view' }),
    );
    expect(
      await screen.findByRole('heading', { name: 'Upcoming events' }),
    ).toBeInTheDocument();
    expect(api.context).toHaveBeenCalledWith(undefined, 'leader');
    expect(demoSession.persona()).toBe('leader');
    expect(
      await screen.findByRole('link', { name: /Sunday gathering/ }),
    ).toHaveAttribute('href', '/app/leader/events/event-one');
  });
  it('does not fetch leader details for a volunteer navigating directly to the URL', async () => {
    renderApp('/app/leader/events/event-one');
    expect(
      await screen.findByRole('heading', {
        name: 'This is the demo leader view.',
      }),
    ).toBeInTheDocument();
    expect(api.leaderEvent).not.toHaveBeenCalled();
    expect(api.teamMembers).not.toHaveBeenCalled();
  });
  it('shows all assignment statuses and server-provided conflict reasons without response controls', async () => {
    enterLeader();
    renderApp('/app/leader/events/event-one');
    expect(await screen.findByText(conflict.message)).toBeInTheDocument();
    const summary = screen.getByRole('region', { name: 'Assignment overview' });
    expect(within(summary).getByText('In conflict')).toBeInTheDocument();
    expect(screen.getAllByTestId('leader-assignment')).toHaveLength(2);
    expect(
      screen.queryByRole('button', { name: /^Accept/ }),
    ).not.toBeInTheDocument();
  });
  it('previews conflicts server-side, invalidates a changed preview, and saves advisories visibly', async () => {
    enterLeader();
    vi.mocked(api.previewAssignment).mockResolvedValue({
      conflicts: [unavailableConflict],
    });
    vi.mocked(api.createAssignment).mockResolvedValue({
      ...assignment,
      roleName: 'Welcome table',
      conflicts: [unavailableConflict],
    });
    renderApp('/app/leader/events/event-one');
    await userEvent.selectOptions(
      await screen.findByLabelText('Team member'),
      member.id,
    );
    await userEvent.type(screen.getByLabelText('Role'), 'Welcome');
    await userEvent.click(
      screen.getByRole('button', { name: 'Preview assignment' }),
    );
    expect(
      await screen.findByRole('heading', { name: 'Schedule preview' }),
    ).toBeInTheDocument();
    expect(api.previewAssignment).toHaveBeenCalledWith(
      context.organizationId,
      event.id,
      { teamMemberId: member.id, roleName: 'Welcome' },
    );
    expect(api.createAssignment).not.toHaveBeenCalled();
    await userEvent.type(screen.getByLabelText('Role'), ' table');
    expect(
      screen.queryByRole('button', { name: 'Save assignment' }),
    ).not.toBeInTheDocument();
    await userEvent.click(
      screen.getByRole('button', { name: 'Preview assignment' }),
    );
    await userEvent.click(
      await screen.findByRole('button', { name: 'Save assignment' }),
    );
    expect(api.createAssignment).toHaveBeenCalledWith(
      context.organizationId,
      event.id,
      { teamMemberId: member.id, roleName: 'Welcome table' },
    );
    expect(
      await screen.findByText(/Assignment saved for Alex Morgan/),
    ).toHaveTextContent('Schedule conflicts are shown below');
    await waitFor(() => expect(api.leaderEvent).toHaveBeenCalledTimes(2));
  });
  it('requires a fresh preview after a save conflict', async () => {
    enterLeader();
    vi.mocked(api.previewAssignment).mockResolvedValue({ conflicts: [] });
    vi.mocked(api.createAssignment).mockRejectedValue(
      new ApiError(409, 'This assignment already exists.'),
    );
    renderApp('/app/leader/events/event-one');
    await userEvent.selectOptions(
      await screen.findByLabelText('Team member'),
      member.id,
    );
    await userEvent.type(screen.getByLabelText('Role'), 'Greeter');
    await userEvent.click(
      screen.getByRole('button', { name: 'Preview assignment' }),
    );
    await userEvent.click(
      await screen.findByRole('button', { name: 'Save assignment' }),
    );
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'already exists',
    );
    expect(
      screen.getByRole('button', { name: 'Preview assignment' }),
    ).toBeEnabled();
    expect(api.createAssignment).toHaveBeenCalledTimes(1);
  });
});

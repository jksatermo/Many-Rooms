import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';
import { api, ApiError } from './api';
import { demoSession } from './session';
import { assignment, context, dashboard, event } from './test/fixtures';

vi.mock('./api', async (importOriginal) => {
  const original = await importOriginal<typeof import('./api')>();
  return {
    ...original,
    api: {
      context: vi.fn(),
      dashboard: vi.fn(),
      events: vi.fn(),
      event: vi.fn(),
      respond: vi.fn(),
    },
  };
});
const renderApp = (path = '/app/events/event-one') =>
  render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  );
beforeEach(() => {
  vi.resetAllMocks();
  vi.mocked(api.context).mockResolvedValue(context);
  vi.mocked(api.dashboard).mockResolvedValue(dashboard);
  vi.mocked(api.events).mockResolvedValue(dashboard.events);
  vi.mocked(api.event).mockResolvedValue(event);
  demoSession.enter();
});

describe('the community demo', () => {
  it('explains the shared development flow before entering the dashboard', async () => {
    demoSession.leave();
    renderApp('/demo');
    expect(
      screen.getByText('This is a shared development demo.'),
    ).toBeInTheDocument();
    await userEvent.click(
      screen.getByRole('button', { name: /Enter the demo/ }),
    );
    expect(
      await screen.findByRole('heading', { name: /Welcome back, Alex/ }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'Can you be there?' }),
    ).toBeInTheDocument();
  });
  it('routes direct application visits to explicit demo entry', async () => {
    demoSession.leave();
    renderApp();
    expect(
      await screen.findByRole('heading', { name: 'Take a look around.' }),
    ).toBeInTheDocument();
  });
  it('provides an actionable error if demo entry cannot reach the API', async () => {
    vi.mocked(api.context).mockRejectedValue(
      new ApiError(0, 'The local API is unavailable.'),
    );
    renderApp('/demo');
    await userEvent.click(
      screen.getByRole('button', { name: /Enter the demo/ }),
    );
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The local API is unavailable.',
    );
    expect(
      screen.getByRole('button', { name: /Enter the demo/ }),
    ).toBeEnabled();
  });
  it('announces loading before event data becomes available', async () => {
    let finish!: (value: typeof event) => void;
    vi.mocked(api.event).mockReturnValue(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    renderApp();
    expect(
      await screen.findByText('Loading the event and its teams…'),
    ).toBeInTheDocument();
    await act(async () => {
      finish(event);
    });
    expect(
      await screen.findByRole('heading', { name: event.title }),
    ).toBeInTheDocument();
  });
  it.each(['Accepted', 'Declined'] as const)(
    'saves a %s response using the current version and updates the visible status',
    async (decision) => {
      vi.mocked(api.respond).mockResolvedValue({
        ...assignment,
        status: decision,
        version: 2,
        respondedAt: '2099-06-20T12:00:00Z',
      });
      renderApp();
      const button = await screen.findByRole('button', {
        name: decision === 'Accepted' ? /Accept/ : /Decline/,
      });
      await userEvent.click(button);
      await waitFor(() =>
        expect(api.respond).toHaveBeenCalledWith(
          context.organizationId,
          assignment,
          decision,
        ),
      );
      const own = screen
        .getAllByTestId('assignment')
        .find((item) => item.dataset.currentUser === 'true')!;
      await waitFor(() => expect(own).toHaveAttribute('data-status', decision));
      expect(screen.getByRole('status')).toHaveTextContent(
        decision === 'Accepted'
          ? 'Your acceptance has been saved'
          : 'Your decline has been saved',
      );
      expect(
        within(own).getByRole('button', { name: new RegExp(decision) }),
      ).toBeDisabled();
    },
  );
  it('only offers response controls for the current volunteer', async () => {
    renderApp();
    await screen.findByRole('heading', { name: event.title });
    const other = screen
      .getAllByTestId('assignment')
      .find((item) => item.dataset.currentUser === 'false')!;
    expect(within(other).queryByRole('button')).not.toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /Accept/ })).toHaveLength(1);
  });
  it('prevents duplicate submissions while saving and preserves the last confirmed status', async () => {
    let finish!: (value: typeof assignment) => void;
    vi.mocked(api.respond).mockReturnValue(
      new Promise((resolve) => {
        finish = resolve;
      }),
    );
    renderApp();
    await userEvent.click(
      await screen.findByRole('button', { name: /Accept/ }),
    );
    expect(screen.getByRole('button', { name: /Saving/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Decline/ })).toBeDisabled();
    expect(screen.getAllByTestId('assignment')[0]).toHaveAttribute(
      'data-status',
      'Pending',
    );
    await act(async () => {
      finish({ ...assignment, status: 'Accepted', version: 2 });
    });
    expect(api.respond).toHaveBeenCalledTimes(1);
  });
  it('keeps the pending status and allows retry after a failed save', async () => {
    vi.mocked(api.respond).mockRejectedValue(
      new ApiError(503, 'The database is temporarily unavailable.'),
    );
    renderApp();
    await userEvent.click(
      await screen.findByRole('button', { name: /Accept/ }),
    );
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The database is temporarily unavailable.',
    );
    expect(screen.getAllByTestId('assignment')[0]).toHaveAttribute(
      'data-status',
      'Pending',
    );
    expect(screen.getByRole('button', { name: /Accept/ })).toBeEnabled();
  });
  it('offers demo entry when the API rejects the demo identity', async () => {
    vi.mocked(api.event).mockRejectedValue(
      new ApiError(401, 'Demo access is unavailable.'),
    );
    renderApp();
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Demo access is unavailable.',
    );
    expect(
      screen.getByRole('link', { name: 'Return to demo entry' }),
    ).toHaveAttribute('href', '/demo');
  });
  it('reloads stale details on 409 and requires another deliberate response', async () => {
    vi.mocked(api.event)
      .mockResolvedValueOnce(event)
      .mockResolvedValueOnce({
        ...event,
        assignments: [{ ...assignment, version: 2, status: 'Declined' }],
      });
    vi.mocked(api.respond).mockRejectedValueOnce(
      new ApiError(409, 'Another person changed this assignment.'),
    );
    renderApp();
    await userEvent.click(
      await screen.findByRole('button', { name: /Accept/ }),
    );
    await waitFor(() => expect(api.event).toHaveBeenCalledTimes(2));
    await waitFor(() =>
      expect(screen.getByTestId('assignment')).toHaveAttribute(
        'data-status',
        'Declined',
      ),
    );
    expect(screen.getByRole('status')).toHaveTextContent(
      'Review them before choosing a response again',
    );
    expect(api.respond).toHaveBeenCalledTimes(1);
    vi.mocked(api.respond).mockResolvedValueOnce({
      ...assignment,
      version: 3,
      status: 'Accepted',
    });
    await userEvent.click(screen.getByRole('button', { name: /Accept/ }));
    expect(api.respond).toHaveBeenLastCalledWith(
      context.organizationId,
      expect.objectContaining({ version: 2 }),
      'Accepted',
    );
  });
  it('closes response controls when the event has begun', async () => {
    vi.mocked(api.event).mockResolvedValue({
      ...event,
      startsAt: '2020-01-01T10:00:00Z',
    });
    renderApp();
    expect(
      await screen.findByText(
        'This event has started, so responses are closed.',
      ),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: /Accept/ }),
    ).not.toBeInTheDocument();
  });
  it('shows an honest empty state when the calendar has no events', async () => {
    vi.mocked(api.events).mockResolvedValue([]);
    renderApp('/app/events');
    expect(
      await screen.findByRole('heading', {
        name: 'The calendar is clear for now.',
      }),
    ).toBeInTheDocument();
  });
});

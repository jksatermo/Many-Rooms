import { afterEach, describe, expect, it, vi } from 'vitest';
import { api, ApiError } from './api';
import { assignment, context } from './test/fixtures';
import { demoSession } from './session';

afterEach(() => {
  vi.unstubAllGlobals();
  demoSession.leave();
});
describe('API boundary', () => {
  it('sends the fixed demo header and versioned response without user-selected identity', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        new Response(JSON.stringify({ ...assignment, status: 'Accepted' })),
      );
    vi.stubGlobal('fetch', fetchMock);
    await api.respond('org/one', assignment, 'Accepted');
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5080/api/organizations/org%2Fone/assignments/assignment-one/response',
      expect.objectContaining({
        method: 'PUT',
        headers: {
          'X-ManyRooms-Demo': 'true',
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ decision: 'Accepted', expectedVersion: 1 }),
      }),
    );
  });
  it('retrieves demo context without claiming an identity', async () => {
    const fetchMock = vi
      .fn()
      .mockImplementation(() =>
        Promise.resolve(new Response(JSON.stringify(context))),
      );
    vi.stubGlobal('fetch', fetchMock);
    expect(await api.context()).toEqual(context);
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5080/api/demo/context',
      expect.objectContaining({ headers: {} }),
    );
  });
  it('preserves problem-details status and detail for conflict handling', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ detail: 'The assignment changed.' }), {
          status: 409,
        }),
      ),
    );
    await expect(
      api.respond('org-one', assignment, 'Declined'),
    ).rejects.toMatchObject({
      status: 409,
      message: 'The assignment changed.',
    });
  });
  it('offers a clear demo message for 401 responses', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response('', { status: 401 })),
    );
    await expect(api.dashboard('org-one')).rejects.toMatchObject({
      status: 401,
      message: expect.stringContaining('Return to demo entry'),
    });
  });
  it('turns network failures into an actionable message', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockRejectedValue(new TypeError('fetch failed')),
    );
    await expect(api.context()).rejects.toBeInstanceOf(ApiError);
    await expect(api.context()).rejects.toMatchObject({
      status: 0,
      message: expect.stringContaining('local API is running'),
    });
  });
  it('uses only the explicit fixed leader persona, with API authorization left to the server', async () => {
    const fetchMock = vi
      .fn()
      .mockImplementation(() =>
        Promise.resolve(new Response(JSON.stringify(context))),
      );
    vi.stubGlobal('fetch', fetchMock);
    demoSession.enter('leader');
    await api.context();
    expect(fetchMock).toHaveBeenLastCalledWith(
      'http://localhost:5080/api/demo/context?persona=leader',
      expect.objectContaining({ headers: {} }),
    );
    await api.leaderEvent('org-one', 'event/one');
    expect(fetchMock).toHaveBeenLastCalledWith(
      'http://localhost:5080/api/organizations/org-one/leader/events/event%2Fone',
      expect.objectContaining({ headers: { 'X-ManyRooms-Demo': 'leader' } }),
    );
  });
  it('updates and deletes unavailable time using its optimistic version, including an empty 204 response', async () => {
    const input = {
      startsAt: '2099-06-21T15:00:00Z',
      endsAt: '2099-06-21T16:00:00Z',
      note: null,
    };
    const current = { ...input, id: 'range/one', version: 4 };
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ ...current, version: 5 })),
      )
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal('fetch', fetchMock);
    await api.updateAvailability('org-one', current, input);
    expect(fetchMock).toHaveBeenLastCalledWith(
      'http://localhost:5080/api/organizations/org-one/availability/range%2Fone',
      expect.objectContaining({
        method: 'PUT',
        body: JSON.stringify({ ...input, expectedVersion: 4 }),
      }),
    );
    await expect(
      api.removeAvailability('org-one', { ...current, version: 5 }),
    ).resolves.toBeUndefined();
    expect(fetchMock).toHaveBeenLastCalledWith(
      'http://localhost:5080/api/organizations/org-one/availability/range%2Fone?expectedVersion=5',
      expect.objectContaining({ method: 'DELETE' }),
    );
  });
});

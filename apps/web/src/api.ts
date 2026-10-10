import type {
  Assignment,
  Dashboard,
  Decision,
  DemoContext,
  EventDetail,
  EventSummary,
} from './types';

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

const baseUrl = (
  import.meta.env.VITE_API_BASE_URL || 'http://localhost:5080'
).replace(/\/$/, '');

async function request<T>(
  path: string,
  options: RequestInit = {},
  demo = true,
): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${baseUrl}${path}`, {
      ...options,
      headers: {
        ...(demo ? { 'X-ManyRooms-Demo': 'true' } : {}),
        ...(options.body ? { 'Content-Type': 'application/json' } : {}),
        ...options.headers,
      },
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError')
      throw error;
    throw new ApiError(
      0,
      'We could not reach Many Rooms. Check your connection and make sure the local API is running.',
    );
  }
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as {
      detail?: string;
      title?: string;
    } | null;
    const detail =
      response.status === 401
        ? 'The development demo is unavailable or your demo session has ended. Return to demo entry to try again.'
        : problem?.detail ||
          problem?.title ||
          'Something went wrong. Please try again.';
    throw new ApiError(response.status, detail);
  }
  return response.json() as Promise<T>;
}

const org = (organizationId: string) =>
  `/api/organizations/${encodeURIComponent(organizationId)}`;
export const api = {
  context: (signal?: AbortSignal) =>
    request<DemoContext>('/api/demo/context', { signal }, false),
  dashboard: (id: string, signal?: AbortSignal) =>
    request<Dashboard>(`${org(id)}/dashboard`, { signal }),
  events: (id: string, signal?: AbortSignal) =>
    request<EventSummary[]>(`${org(id)}/events`, { signal }),
  event: (id: string, eventId: string, signal?: AbortSignal) =>
    request<EventDetail>(`${org(id)}/events/${encodeURIComponent(eventId)}`, {
      signal,
    }),
  respond: (id: string, assignment: Assignment, decision: Decision) =>
    request<Assignment>(
      `${org(id)}/assignments/${encodeURIComponent(assignment.id)}/response`,
      {
        method: 'PUT',
        body: JSON.stringify({ decision, expectedVersion: assignment.version }),
      },
    ),
};

import type {
  Assignment,
  Dashboard,
  Decision,
  DemoContext,
  DemoPersona,
  EventDetail,
  EventSummary,
  NewAssignment,
  ScheduleConflict,
  TeamMember,
  Unavailability,
  UnavailabilityInput,
} from './types';
import { demoSession } from './session';

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
        ...(demo
          ? {
              'X-ManyRooms-Demo':
                demoSession.persona() === 'leader' ? 'leader' : 'true',
            }
          : {}),
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
  return response.status === 204
    ? (undefined as T)
    : (response.json() as Promise<T>);
}

const org = (organizationId: string) =>
  `/api/organizations/${encodeURIComponent(organizationId)}`;
export const api = {
  context: (
    signal?: AbortSignal,
    persona: DemoPersona = demoSession.persona(),
  ) =>
    request<DemoContext>(
      `/api/demo/context${persona === 'leader' ? '?persona=leader' : ''}`,
      { signal },
      false,
    ),
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
  availability: (id: string, signal?: AbortSignal) =>
    request<Unavailability[]>(`${org(id)}/availability`, { signal }),
  createAvailability: (id: string, input: UnavailabilityInput) =>
    request<Unavailability>(`${org(id)}/availability`, {
      method: 'POST',
      body: JSON.stringify(input),
    }),
  updateAvailability: (
    id: string,
    current: Unavailability,
    input: UnavailabilityInput,
  ) =>
    request<Unavailability>(
      `${org(id)}/availability/${encodeURIComponent(current.id)}`,
      {
        method: 'PUT',
        body: JSON.stringify({ ...input, expectedVersion: current.version }),
      },
    ),
  removeAvailability: (id: string, current: Unavailability) =>
    request<void>(
      `${org(id)}/availability/${encodeURIComponent(current.id)}?expectedVersion=${current.version}`,
      { method: 'DELETE' },
    ),
  leaderEvent: (id: string, eventId: string, signal?: AbortSignal) =>
    request<EventDetail>(
      `${org(id)}/leader/events/${encodeURIComponent(eventId)}`,
      { signal },
    ),
  teamMembers: (id: string, signal?: AbortSignal) =>
    request<TeamMember[]>(`${org(id)}/leader/team-members`, { signal }),
  previewAssignment: (id: string, eventId: string, input: NewAssignment) =>
    request<{ conflicts: ScheduleConflict[] }>(
      `${org(id)}/leader/events/${encodeURIComponent(eventId)}/assignments/preview`,
      { method: 'POST', body: JSON.stringify(input) },
    ),
  createAssignment: (id: string, eventId: string, input: NewAssignment) =>
    request<Assignment>(
      `${org(id)}/leader/events/${encodeURIComponent(eventId)}/assignments`,
      { method: 'POST', body: JSON.stringify(input) },
    ),
};

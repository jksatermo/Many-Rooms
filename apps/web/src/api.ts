import type {
  Assignment,
  AssignmentHistory,
  TeamSummary,
  TeamDetail,
  OrganizationMember,
  StaffingRequirement,
  StaffingInput,
  ReplacementCandidate,
  EventInput,
  EventTemplateInput,
  EventTemplate,
  GenerationResult,
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

  teams: (id: string, signal?: AbortSignal) =>
    request<TeamSummary[]>(`${org(id)}/leader/teams`, { signal }),
  team: (id: string, teamId: string, signal?: AbortSignal) =>
    request<TeamDetail>(
      `${org(id)}/leader/teams/${encodeURIComponent(teamId)}`,
      { signal },
    ),
  organizationMembers: (id: string, signal?: AbortSignal) =>
    request<OrganizationMember[]>(`${org(id)}/leader/organization-members`, {
      signal,
    }),
  addTeamMember: (
    id: string,
    teamId: string,
    membershipId: string,
    positionName: string | null,
  ) =>
    request<TeamMember>(
      `${org(id)}/leader/teams/${encodeURIComponent(teamId)}/members`,
      { method: 'POST', body: JSON.stringify({ membershipId, positionName }) },
    ),
  updateTeamMember: (
    id: string,
    member: TeamMember,
    positionName: string | null,
  ) =>
    request<TeamMember>(
      `${org(id)}/leader/teams/${encodeURIComponent(member.teamId)}/members/${encodeURIComponent(member.id)}`,
      {
        method: 'PUT',
        body: JSON.stringify({ positionName, expectedVersion: member.version }),
      },
    ),
  removeTeamMember: (id: string, member: TeamMember) =>
    request<void>(
      `${org(id)}/leader/teams/${encodeURIComponent(member.teamId)}/members/${encodeURIComponent(member.id)}?expectedVersion=${member.version}`,
      { method: 'DELETE' },
    ),
  staffing: (id: string, eventId: string, signal?: AbortSignal) =>
    request<StaffingRequirement[]>(
      `${org(id)}/leader/events/${encodeURIComponent(eventId)}/staffing`,
      { signal },
    ),
  createStaffing: (id: string, eventId: string, input: StaffingInput) =>
    request<StaffingRequirement>(
      `${org(id)}/leader/events/${encodeURIComponent(eventId)}/staffing`,
      { method: 'POST', body: JSON.stringify(input) },
    ),
  updateStaffing: (
    id: string,
    current: StaffingRequirement,
    input: Omit<StaffingInput, 'teamId'>,
  ) =>
    request<StaffingRequirement>(
      `${org(id)}/leader/events/${encodeURIComponent(current.eventId)}/staffing/${encodeURIComponent(current.id)}`,
      {
        method: 'PUT',
        body: JSON.stringify({ ...input, expectedVersion: current.version }),
      },
    ),
  removeStaffing: (id: string, current: StaffingRequirement) =>
    request<void>(
      `${org(id)}/leader/events/${encodeURIComponent(current.eventId)}/staffing/${encodeURIComponent(current.id)}?expectedVersion=${current.version}`,
      { method: 'DELETE' },
    ),
  previewAssignmentEdit: (
    id: string,
    current: Assignment,
    input: NewAssignment,
  ) =>
    request<{ conflicts: ScheduleConflict[] }>(
      `${org(id)}/leader/assignments/${encodeURIComponent(current.id)}/preview`,
      {
        method: 'POST',
        body: JSON.stringify({ ...input, expectedVersion: current.version }),
      },
    ),
  updateAssignment: (id: string, current: Assignment, input: NewAssignment) =>
    request<Assignment>(
      `${org(id)}/leader/assignments/${encodeURIComponent(current.id)}`,
      {
        method: 'PUT',
        body: JSON.stringify({ ...input, expectedVersion: current.version }),
      },
    ),
  cancelAssignment: (id: string, current: Assignment) =>
    request<Assignment>(
      `${org(id)}/leader/assignments/${encodeURIComponent(current.id)}?expectedVersion=${current.version}`,
      { method: 'DELETE' },
    ),
  eventHistory: (id: string, eventId: string, signal?: AbortSignal) =>
    request<AssignmentHistory[]>(
      `${org(id)}/leader/events/${encodeURIComponent(eventId)}/history`,
      { signal },
    ),
  assignmentHistory: (id: string, assignmentId: string, signal?: AbortSignal) =>
    request<AssignmentHistory[]>(
      `${org(id)}/leader/assignments/${encodeURIComponent(assignmentId)}/history`,
      { signal },
    ),
  conflicts: (id: string, signal?: AbortSignal) =>
    request<Assignment[]>(`${org(id)}/leader/conflicts`, { signal }),
  replacementCandidates: (
    id: string,
    assignmentId: string,
    signal?: AbortSignal,
  ) =>
    request<ReplacementCandidate[]>(
      `${org(id)}/leader/assignments/${encodeURIComponent(assignmentId)}/replacement-candidates`,
      { signal },
    ),
  createEvent: (id: string, input: EventInput) =>
    request<EventDetail>(`${org(id)}/leader/events`, {
      method: 'POST',
      body: JSON.stringify(input),
    }),
  eventTemplates: (id: string, signal?: AbortSignal) =>
    request<EventTemplate[]>(`${org(id)}/leader/event-templates`, { signal }),
  createEventTemplate: (id: string, input: EventTemplateInput) =>
    request<EventTemplate>(`${org(id)}/leader/event-templates`, {
      method: 'POST',
      body: JSON.stringify(input),
    }),
  generateEvents: (id: string, templateId: string, throughDate: string) =>
    request<GenerationResult>(
      `${org(id)}/leader/event-templates/${encodeURIComponent(templateId)}/generate`,
      { method: 'POST', body: JSON.stringify({ throughDate }) },
    ),
};

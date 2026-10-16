export interface DemoContext {
  organizationId: string;
  organizationName: string;
  userId: string;
  displayName: string;
  timeZone: string;
  role: "Volunteer" | "Leader";
}

export interface ScheduleConflict {
  type: "OverlappingAssignment" | "Unavailable";
  message: string;
  startsAt: string;
  endsAt: string;
  relatedEventId: string | null;
  relatedEventTitle: string | null;
  relatedAssignmentId: string | null;
  availabilityId: string | null;
}
export interface Assignment {
  id: string;
  eventId: string;
  eventTitle: string;
  eventStartsAt: string;
  eventEndsAt: string;
  teamId: string;
  teamMemberId: string;
  teamName: string;
  roleName: string;
  volunteerId: string;
  volunteerName: string;
  status: "Pending" | "Accepted" | "Declined";
  isCancelled: boolean;
  respondedAt: string | null;
  version: number;
  isCurrentUser: boolean;
  conflicts: ScheduleConflict[];
}
export interface EventSummary {
  id: string;
  title: string;
  startsAt: string;
  endsAt: string;
  location: string;
}
export interface EventDetails extends EventSummary {
  description: string;
  assignments: Assignment[];
}
export interface Dashboard {
  organization: { id: string; name: string; timeZone: string };
  currentUser: { id: string; displayName: string };
  summary: {
    upcomingEvents: number;
    pendingResponses: number;
    acceptedAssignments: number;
  };
  events: EventSummary[];
  assignments: Assignment[];
}
export interface Unavailability {
  id: string;
  startsAt: string;
  endsAt: string;
  note: string | null;
  version: number;
}
export interface AvailabilityInput {
  startsAt: string;
  endsAt: string;
  note: string | null;
}
export class ApiError extends Error {
  readonly status: number;
  constructor(status: number, message: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

/** Development identity only; no token or secret is stored on the device. */
export function createApi(baseUrl: string, fetcher: typeof fetch = fetch) {
  const base = baseUrl.replace(/\/$/, "");
  const organization = (id: string) =>
    `/api/organizations/${encodeURIComponent(id)}`;
  async function request<T>(
    path: string,
    options: {
      method?: string;
      body?: unknown;
      signal?: AbortSignal;
      demo?: boolean;
    } = {},
  ): Promise<T> {
    let response: Response;
    try {
      response = await fetcher(`${base}${path}`, {
        method: options.method ?? "GET",
        headers: {
          ...(options.demo !== false ? { "X-ManyRooms-Demo": "true" } : {}),
          ...(options.body !== undefined
            ? { "Content-Type": "application/json" }
            : {}),
        },
        body:
          options.body !== undefined ? JSON.stringify(options.body) : undefined,
        signal: options.signal,
      });
    } catch (failure) {
      if (options.signal?.aborted) throw failure;
      throw new ApiError(
        0,
        "We couldn’t connect. Check the API connection and try again.",
      );
    }
    if (!response.ok) {
      if (response.status === 401) {
        throw new ApiError(
          401,
          "The development demo is unavailable. Check that the API is running with demo access enabled.",
        );
      }
      if (response.status === 409) {
        throw new ApiError(
          409,
          "This schedule changed since you opened it. Refresh and review the latest details before trying again.",
        );
      }
      if (response.status === 403) {
        throw new ApiError(
          403,
          "You don’t have access to make this change in this community.",
        );
      }
      if (response.status === 404) {
        throw new ApiError(
          404,
          options.demo === false
            ? "The development demo is unavailable. Check that the API is running with demo access enabled."
            : "This item is no longer available. Refresh your schedule to see the latest details.",
        );
      }
      if (response.status === 400) {
        throw new ApiError(
          400,
          "Please check your dates and details, then try again.",
        );
      }
      throw new ApiError(
        response.status,
        "We couldn’t complete that request. Check the API connection and try again.",
      );
    }
    return response.status === 204
      ? (undefined as T)
      : (response.json() as Promise<T>);
  }
  return {
    context: (signal?: AbortSignal) =>
      request<DemoContext>("/api/demo/context", { demo: false, signal }),
    dashboard: (organizationId: string, signal?: AbortSignal) =>
      request<Dashboard>(`${organization(organizationId)}/dashboard`, {
        signal,
      }),
    event: (organizationId: string, eventId: string, signal?: AbortSignal) =>
      request<EventDetails>(
        `${organization(organizationId)}/events/${encodeURIComponent(eventId)}`,
        { signal },
      ),
    respond: (
      organizationId: string,
      assignmentId: string,
      decision: "Accepted" | "Declined",
      expectedVersion: number,
    ) =>
      request<Assignment>(
        `${organization(organizationId)}/assignments/${encodeURIComponent(assignmentId)}/response`,
        { method: "PUT", body: { decision, expectedVersion } },
      ),
    availability: (organizationId: string, signal?: AbortSignal) =>
      request<Unavailability[]>(
        `${organization(organizationId)}/availability`,
        { signal },
      ),
    createAvailability: (organizationId: string, input: AvailabilityInput) =>
      request<Unavailability>(`${organization(organizationId)}/availability`, {
        method: "POST",
        body: input,
      }),
    updateAvailability: (
      organizationId: string,
      id: string,
      input: AvailabilityInput,
      expectedVersion: number,
    ) =>
      request<Unavailability>(
        `${organization(organizationId)}/availability/${encodeURIComponent(id)}`,
        { method: "PUT", body: { ...input, expectedVersion } },
      ),
    deleteAvailability: (
      organizationId: string,
      id: string,
      expectedVersion: number,
    ) =>
      request<void>(
        `${organization(organizationId)}/availability/${encodeURIComponent(id)}?expectedVersion=${expectedVersion}`,
        { method: "DELETE" },
      ),
  };
}

export interface DemoContext {
  organizationId: string;
  organizationName: string;
  userId: string;
  displayName: string;
  timeZone: string;
}

export interface Dashboard {
  organization: { id: string; name: string; timeZone: string };
  currentUser: { id: string; displayName: string };
  summary: {
    upcomingEvents: number;
    pendingResponses: number;
    acceptedAssignments: number;
  };
  events: {
    id: string;
    title: string;
    startsAt: string;
    endsAt: string;
    location: string;
  }[];
  assignments: {
    id: string;
    eventTitle: string;
    eventStartsAt: string;
    teamName: string;
    roleName: string;
    status: "Pending" | "Accepted" | "Declined";
  }[];
}

/** Demo identity is explicitly requested; no token or secret is stored on the device. */
export function createApi(baseUrl: string, fetcher: typeof fetch = fetch) {
  const base = baseUrl.replace(/\/$/, "");
  async function get<T>(
    path: string,
    demo: boolean,
    signal?: AbortSignal,
  ): Promise<T> {
    const response = await fetcher(`${base}${path}`, {
      headers: demo ? { "X-ManyRooms-Demo": "true" } : {},
      signal,
    });
    if (!response.ok) {
      if (response.status === 401 || response.status === 404) {
        throw new Error(
          "The development demo is unavailable. Check that the API is running with demo access enabled.",
        );
      }
      throw new Error(
        "We couldn’t load your community. Check the API connection and try again.",
      );
    }
    return response.json() as Promise<T>;
  }
  return {
    context: (signal?: AbortSignal) =>
      get<DemoContext>("/api/demo/context", false, signal),
    dashboard: (organizationId: string, signal?: AbortSignal) =>
      get<Dashboard>(
        `/api/organizations/${encodeURIComponent(organizationId)}/dashboard`,
        true,
        signal,
      ),
  };
}

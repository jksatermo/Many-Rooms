export type AssignmentStatus = 'Pending' | 'Accepted' | 'Declined';
export type Decision = Exclude<AssignmentStatus, 'Pending'>;

export interface DemoContext {
  organizationId: string;
  organizationName: string;
  userId: string;
  displayName: string;
  timeZone: string;
}

export interface Assignment {
  id: string;
  eventId: string;
  eventTitle: string;
  eventStartsAt: string;
  teamName: string;
  roleName: string;
  volunteerId: string;
  volunteerName: string;
  status: AssignmentStatus;
  respondedAt: string | null;
  version: number;
  isCurrentUser: boolean;
}

export interface EventSummary {
  id: string;
  title: string;
  startsAt: string;
  endsAt: string;
  location: string;
  assignmentCount: number;
  pendingResponseCount: number;
}

export interface EventDetail {
  id: string;
  title: string;
  description: string;
  startsAt: string;
  endsAt: string;
  location: string;
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

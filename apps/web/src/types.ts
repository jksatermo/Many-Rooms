export type AssignmentStatus = 'Pending' | 'Accepted' | 'Declined';
export type Decision = Exclude<AssignmentStatus, 'Pending'>;
export type DemoPersona = 'volunteer' | 'leader';

export interface DemoContext {
  organizationId: string;
  organizationName: string;
  userId: string;
  displayName: string;
  timeZone: string;
  role: 'Volunteer' | 'Leader';
}

export interface Assignment {
  id: string;
  eventId: string;
  eventTitle: string;
  eventStartsAt: string;
  eventEndsAt: string;
  teamName: string;
  roleName: string;
  volunteerId: string;
  volunteerName: string;
  status: AssignmentStatus;
  respondedAt: string | null;
  version: number;
  isCurrentUser: boolean;
  conflicts: ScheduleConflict[];
}

export interface ScheduleConflict {
  type: 'OverlappingAssignment' | 'Unavailable';
  message: string;
  startsAt: string;
  endsAt: string;
  relatedEventId: string | null;
  relatedEventTitle: string | null;
  relatedAssignmentId: string | null;
  availabilityId: string | null;
}

export interface UnavailabilityInput {
  startsAt: string;
  endsAt: string;
  note: string | null;
}
export interface Unavailability extends UnavailabilityInput {
  id: string;
  version: number;
}

export interface TeamMember {
  id: string;
  teamId: string;
  teamName: string;
  membershipId: string;
  volunteerId: string;
  volunteerName: string;
}
export interface NewAssignment {
  teamMemberId: string;
  roleName: string;
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

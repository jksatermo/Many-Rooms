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
  teamId: string;
  teamMemberId: string;
  isCancelled: boolean;
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
  positionName: string | null;
  version: number;
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

export interface TeamSummary {
  id: string;
  name: string;
  description: string | null;
  memberCount: number;
  upcomingAssignmentCount: number;
}
export interface TeamDetail {
  id: string;
  name: string;
  description: string | null;
  members: TeamMember[];
  upcomingAssignments: Assignment[];
}
export interface OrganizationMember {
  id: string;
  volunteerId: string;
  volunteerName: string;
}
export interface StaffingRequirement {
  id: string;
  eventId: string;
  teamId: string;
  teamName: string;
  roleName: string | null;
  desiredCount: number;
  version: number;
  assigned: number;
  accepted: number;
  pending: number;
  declined: number;
  conflicted: number;
  openSpots: number;
}
export interface StaffingInput {
  teamId: string;
  roleName: string | null;
  desiredCount: number;
}
export interface AssignmentHistory {
  id: string;
  assignmentId: string;
  action: string;
  occurredAt: string;
  actorName: string | null;
  previousVolunteerName: string | null;
  volunteerName: string;
  previousRoleName: string | null;
  roleName: string;
  assignmentVersion: number;
}
export interface ReplacementCandidate {
  teamMemberId: string;
  membershipId: string;
  volunteerId: string;
  volunteerName: string;
  positionName: string | null;
  roleMatch: boolean;
  upcomingAssignmentCount: number;
}
export interface EventInput {
  title: string;
  description: string;
  location: string;
  startsAt: string;
  endsAt: string;
}
export interface EventTemplateInput {
  title: string;
  description: string;
  location: string;
  dayOfWeek: number;
  localStartTime: string;
  durationMinutes: number;
  startsOn: string;
}
export interface EventTemplate extends EventTemplateInput {
  id: string;
  timeZone: string;
}
export interface GenerationResult {
  createdEvents: EventSummary[];
  existingCount: number;
  skippedDates: string[];
}

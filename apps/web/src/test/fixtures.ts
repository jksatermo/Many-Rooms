import type { Assignment, Dashboard, DemoContext, EventDetail } from '../types';

export const context: DemoContext = {
  organizationId: 'org-one',
  organizationName: 'Cedar Grove Community',
  userId: 'alex',
  displayName: 'Alex Morgan',
  timeZone: 'America/Chicago',
  role: 'Volunteer',
};
export const assignment: Assignment = {
  id: 'assignment-one',
  eventId: 'event-one',
  eventTitle: 'Sunday gathering',
  eventStartsAt: '2099-06-21T15:00:00Z',
  eventEndsAt: '2099-06-21T17:00:00Z',
  teamName: 'Welcome team',
  teamId: 'team-one',
  teamMemberId: 'team-member-one',
  isCancelled: false,
  roleName: 'Greeter',
  volunteerId: 'alex',
  volunteerName: 'Alex Morgan',
  status: 'Pending',
  respondedAt: null,
  version: 1,
  isCurrentUser: true,
  conflicts: [],
};
export const event: EventDetail = {
  id: 'event-one',
  title: 'Sunday gathering',
  description: 'A morning to connect and share a meal.',
  startsAt: assignment.eventStartsAt,
  endsAt: '2099-06-21T17:00:00Z',
  location: 'Main hall',
  assignments: [
    assignment,
    {
      ...assignment,
      id: 'assignment-other',
      volunteerId: 'jordan',
      volunteerName: 'Jordan Rivera',
      roleName: 'Host',
      status: 'Accepted',
      isCurrentUser: false,
    },
  ],
};
export const dashboard: Dashboard = {
  organization: {
    id: context.organizationId,
    name: context.organizationName,
    timeZone: context.timeZone,
  },
  currentUser: { id: context.userId, displayName: context.displayName },
  summary: { upcomingEvents: 1, pendingResponses: 1, acceptedAssignments: 0 },
  events: [
    {
      id: event.id,
      title: event.title,
      startsAt: event.startsAt,
      endsAt: event.endsAt,
      location: event.location,
      assignmentCount: 2,
      pendingResponseCount: 1,
    },
  ],
  assignments: [assignment],
};

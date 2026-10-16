# Scheduling rules

Milestone 3 builds leader scheduling on Milestone 2’s explicit unavailable ranges and shared conflict detector. Recurring availability and travel-time rules are not implemented.

## Time convention

- Events and availability represent absolute instants. PostgreSQL uses `timestamp with time zone`; application persistence normalizes to UTC.
- API timestamps use ISO 8601 with `Z` or a numeric offset. A timestamp without an offset is rejected for new availability input rather than interpreted using the server’s timezone.
- Event and conflict descriptions display in the organization’s configured IANA timezone. Seeded communities use `America/Chicago`.
- Availability forms label their input timezone. Device-local input is converted to an offset-bearing UTC timestamp before submission. The stored range does not move when a user travels or changes devices.
- Availability input rejects nonexistent local times during a daylight-saving jump. For a repeated hour when clocks move back, JavaScript Date selects the earlier occurrence; the form does not yet provide an offset selector.
- Ranges are **half-open**: `[start, end)`. End must be strictly later than start. Two ranges overlap exactly when `first.Start < second.End && second.Start < first.End`.

For example, 09:00–10:00 and 10:00–11:00 are adjacent and do not conflict. 09:00–10:30 and 10:00–11:00 conflict during 10:00–10:30. Comparisons use instants, so equivalent values expressed with different offsets behave identically.

## Which assignments count?

Pending and accepted assignments both participate: an unanswered invitation may still become a commitment. Declined and cancelled assignments do not reserve time and return no conflicts. Cancellation is a separate flag; the existing Pending/Accepted/Declined response vocabulary remains stable for both clients. Multiple roles within the same event are not treated as conflicting events; that would require more detailed role/task timing than this model has.

Two assignment ranges are compared only for the same volunteer membership and organization, across any of that person’s teams. Another organization’s events and availability never contribute to the result. This deliberate boundary protects tenant isolation; cross-organization personal calendars are a separate future capability.

An active assignment conflicts with an unavailable range owned by the same organization membership. Exact overlap, partial overlap, and containment all use the same interval rule.

## Structured, advisory results

Assignment DTOs include `conflicts` with a stable `type` (`OverlappingAssignment` or `Unavailable`), a concise message, the overlapping interval, and the relevant event/assignment or availability identifier. The client renders the server’s result; it does not independently decide whether a conflict exists.

Conflicts are calculated from current records on reads and after writes, not stored as flags that can become stale. An assignment preview is a snapshot; assignment creation evaluates conflicts again before returning its result. Another client may change the schedule immediately afterward, so clients refresh on return/focus and after edits.

A conflict does not cancel an assignment, change a response, or prevent Accept/Decline. The leader can still create an assignment after reviewing an advisory. Leaders can resolve a conflict by reassigning or cancelling the assignment. These operations do not send notifications; communication remains the leader’s responsibility in this milestone.

## Ownership and privacy

Availability is attached to an **OrganizationMembership**, with a same-organization composite foreign key. Volunteer-facing endpoints always use the current member; they do not accept a caller-selected owner. Updates and deletes require the current version. Stale edits return 409 so a client can reload and ask the user to decide again.

Availability notes are optional, trimmed, and limited to 500 characters. Notes are returned only to the owner through their availability endpoint. Other volunteers receive no conflict details for another person’s assignments. Leaders receive the type and overlapping times needed to understand a conflict, but not the private note.

The leader view uses a role stored in the organization membership. The demo header selects a fictional identity; it does not supply a trusted role. Production authentication will replace that identity mechanism while retaining membership, ownership, and role checks.

## Team membership and positions

A team member connects an organization membership to an existing team. The optional position is an organization-entered title, not a church-specific enum or a verified qualification. Removing a team member deactivates the row so historical assignments remain readable. Removal returns 409 while the member has future, noncancelled Pending/Accepted assignments; resolve those assignments first. Declined and past assignments do not block removal. Adding the person back reactivates the same row with a new version.

Leader team/member, staffing, assignment, and response mutations coordinate through a PostgreSQL organization transaction lock as well as optimistic versions. This prevents a member-removal/assignment-create race and serializes conflicting staffing definitions. It is a pragmatic single-database mechanism, not a distributed scheduling service.

## Staffing requirements

An event may request a desired number of volunteers from a team, optionally for a named role. Counts are server-calculated from current assignments and distinct memberships:

- **Needed:** the configured desired count, from 1 to 100.
- **Assigned:** Pending plus Accepted volunteers, excluding cancellations.
- **Accepted, Pending, Declined:** separate response totals; declined invitations do not fill a spot.
- **Conflicted:** active assigned volunteers with server-detected conflicts; this overlaps the assigned total.
- **Open:** `max(0, Needed - Assigned)`. Overfilling is advisory.

Roles match after trimming and case-insensitive normalization. A whole-team requirement counts all roles in that team; it cannot coexist with named-role requirements for the same event/team. Duplicate normalized requirements are rejected. Removing a requirement does not cancel assignments.

## Replacement candidates and conflict center

The leader conflict center lists future noncancelled assignments with an overlap/unavailable conflict, plus declined assignments needing review. A declined assignment can have an empty conflict array because it no longer reserves time.

Replacement candidates come from active members of the same team and organization. Exclude the current volunteer, anyone unavailable during the event, anyone with an overlapping Pending/Accepted noncancelled assignment in another event, and anyone already invited to the same event/team/normalized role (including a declined invitation). Other organizations’ schedules never affect suggestions or leak through workload counts.

Order candidates deterministically by:

1. Position matches the requested role, case-insensitively, first.
2. Fewest noncancelled Pending/Accepted assignments starting in the 28-day window beginning at the target event.
3. Volunteer name, ordinal case-insensitive, then membership ID as a stable tie-breaker.

A role match is a preference, not a qualification assessment. Suggestions are a snapshot; saving revalidates membership and returns current advisory conflicts. Leaders may also preview another active member of the same team. No volunteer is automatically reassigned or notified.

## Assignment changes and history

Creation records an actor and a Pending assignment. Reassignment keeps the assignment ID, moves it to an active member of the same team, resets its response to Pending, clears its response timestamp, and increments its version. Role edits also require a fresh response. Cancellation is soft: it increments the version and keeps the assignment visible with a cancellation label, but excludes it from conflicts, workload, and staffing.

Every change requires the current version. Stale changes and edits/responses to cancelled or started assignments return 409. A former volunteer cannot respond after reassignment. Clients refresh after edits, on return/focus, and after stale-state errors; they do not silently retry decisions.

AssignmentHistory records Created, Reassigned, RoleChanged, Cancelled, Accepted, and Declined with the actor, time, version, and relevant before/after name and role snapshots in the same transaction. Response rows remain immutable. Combined volunteer/role edits produce one Reassigned entry containing both changes. Legacy assignments receive an Imported entry with an unknown actor; recorded legacy responses retain only their known response actors. This is a focused product history, not a general compliance audit system.

## Weekly events and time zones

A weekly template stores a weekday, local start time, duration, start date, and a copy of the organization’s validated IANA timezone. Templates are immutable in this milestone. Generation creates ordinary Event rows linked to the template and local occurrence date; those events use the same staffing, assignments, and conflict rules as one-time events.

Generation starts at the later of the template start date and today in its timezone. The inclusive requested range is limited to 84 calendar days (12 weeks). A template/date uniqueness constraint and serialized generation make repeated and concurrent requests idempotent. Earlier historical dates are outside the generation window.

- A nonexistent wall time during a daylight-saving jump is skipped and reported.
- A repeated wall time chooses the earlier UTC occurrence (the larger UTC offset).
- Duration is elapsed minutes in UTC, from 15 to 720 minutes.
- Already-started occurrences are not newly generated. `skippedDates` includes those dates and DST gaps; an already-persisted matching occurrence instead contributes to `existingCount`.
- Staffing and volunteer assignments are not copied from a template. Configure each generated event deliberately.

One-time event input requires ISO timestamps with explicit offsets, a future start, and an end after the start. Device-local web inputs are labeled and converted before submission. Events are displayed in the organization timezone.

## Forward migration and seed

The first two migrations remain unchanged. Milestone 3 adds team-member state/positions/versions, staffing requirements, assignment cancellation/history, and weekly templates/occurrence links. Existing event dates, assignments, decisions, and response rows are retained. History backfill does not invent an original leader actor.

New fictional scheduling examples are added once without moving old events or resetting responses. Repeated seed runs preserve edited requirements, removed/deactivated memberships, responses, and cancelled/reassigned assignments. The existing availability seed marker continues to prevent removed unavailable ranges from being resurrected.

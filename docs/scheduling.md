# Availability and scheduling conflicts

Milestone 2 adds explicit unavailable ranges. It does not attempt to model recurring availability, travel time, or all the ways a community schedules people.

## Time convention

- Events and availability represent absolute instants. PostgreSQL uses `timestamp with time zone`; application persistence normalizes to UTC.
- API timestamps use ISO 8601 with `Z` or a numeric offset. A timestamp without an offset is rejected for new availability input rather than interpreted using the server’s timezone.
- Event and conflict descriptions display in the organization’s configured IANA timezone. Seeded communities use `America/Chicago`.
- Availability forms label their input timezone. Device-local input is converted to an offset-bearing UTC timestamp before submission. The stored range does not move when a user travels or changes devices.
- Availability input rejects nonexistent local times during a daylight-saving jump. For a repeated hour when clocks move back, JavaScript Date selects the earlier occurrence; the form does not yet provide an offset selector.
- Ranges are **half-open**: `[start, end)`. End must be strictly later than start. Two ranges overlap exactly when `first.Start < second.End && second.Start < first.End`.

For example, 09:00–10:00 and 10:00–11:00 are adjacent and do not conflict. 09:00–10:30 and 10:00–11:00 conflict during 10:00–10:30. Comparisons use instants, so equivalent values expressed with different offsets behave identically.

## Which assignments count?

Pending and accepted assignments both participate: an unanswered invitation may still become a commitment. Declined assignments do not reserve time and return no conflicts. Multiple roles within the same event are not treated as conflicting events; that would require more detailed role/task timing than this model has.

Two assignment ranges are compared only for the same volunteer membership and organization, across any of that person’s teams. Another organization’s events and availability never contribute to the result. This deliberate boundary protects tenant isolation; cross-organization personal calendars are a separate future capability.

An active assignment conflicts with an unavailable range owned by the same organization membership. Exact overlap, partial overlap, and containment all use the same interval rule.

## Structured, advisory results

Assignment DTOs include `conflicts` with a stable `type` (`OverlappingAssignment` or `Unavailable`), a concise message, the overlapping interval, and the relevant event/assignment or availability identifier. The client renders the server’s result; it does not independently decide whether a conflict exists.

Conflicts are calculated from current records on reads and after writes, not stored as flags that can become stale. An assignment preview is a snapshot; assignment creation evaluates conflicts again before returning its result. Another client may change the schedule immediately afterward, so clients refresh on return/focus and after edits.

A conflict does not cancel an assignment, change a response, or prevent Accept/Decline. The leader can still create an assignment after reviewing an advisory. Resolving a conflict through reassignment, discussion, or notifications remains future work.

## Ownership and privacy

Availability is attached to an **OrganizationMembership**, with a same-organization composite foreign key. Volunteer-facing endpoints always use the current member; they do not accept a caller-selected owner. Updates and deletes require the current version. Stale edits return 409 so a client can reload and ask the user to decide again.

Availability notes are optional, trimmed, and limited to 500 characters. Notes are returned only to the owner through their availability endpoint. Other volunteers receive no conflict details for another person’s assignments. Leaders receive the type and overlapping times needed to understand a conflict, but not the private note.

The leader view uses a role stored in the organization membership. The demo header selects a fictional identity; it does not supply a trusted role. Production authentication will replace that identity mechanism while retaining membership, ownership, and role checks.

## Forward migration and seed

The original scheduling migration remains unchanged. The new migration adds `VolunteerUnavailability`, including UTC ranges, optional notes, timestamps, an optimistic version, useful scoped indexes, date/version checks, and a composite membership foreign key.

The seed adds conflict scenarios to an existing Milestone 1 database without resetting saved responses. Its one-time upgrade marker prevents repeated seed runs from resurrecting availability that a volunteer deliberately removed. Recreating the local demo database generates a fresh set of future event dates when needed.

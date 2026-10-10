using ManyRooms.Domain;

namespace ManyRooms.Application;

/// <summary>Shared, deterministic scheduling policy. Ranges are [start, end), never local wall times.</summary>
public static class ScheduleConflictDetector
{
    public static IReadOnlyList<ScheduleConflictDto> Detect(VolunteerAssignment target,
        IEnumerable<VolunteerAssignment> assignments, IEnumerable<VolunteerUnavailability> unavailable)
    {
        if (target.Status == AssignmentStatus.Declined) return [];
        var conflicts = new List<ScheduleConflictDto>();
        foreach (var other in assignments)
        {
            if (other.OrganizationId != target.OrganizationId || other.Id == target.Id ||
                other.EventId == target.EventId || other.Status == AssignmentStatus.Declined ||
                other.TeamMember.MembershipId != target.TeamMember.MembershipId ||
                !Overlaps(target.Event.StartsAt, target.Event.EndsAt, other.Event.StartsAt, other.Event.EndsAt)) continue;
            conflicts.Add(new("OverlappingAssignment", $"Overlaps an assignment at {other.Event.Title}.",
                Max(target.Event.StartsAt, other.Event.StartsAt), Min(target.Event.EndsAt, other.Event.EndsAt),
                other.EventId, other.Event.Title, other.Id, null));
        }
        foreach (var entry in unavailable)
        {
            if (entry.OrganizationId != target.OrganizationId || entry.MembershipId != target.TeamMember.MembershipId ||
                !Overlaps(target.Event.StartsAt, target.Event.EndsAt, entry.StartsAt, entry.EndsAt)) continue;
            // A private availability note never enters this shared projection, even for leaders.
            conflicts.Add(new("Unavailable", "Overlaps an unavailable time.",
                Max(target.Event.StartsAt, entry.StartsAt), Min(target.Event.EndsAt, entry.EndsAt),
                null, null, null, entry.Id));
        }
        return conflicts.OrderBy(x => x.StartsAt).ThenBy(x => x.Type, StringComparer.Ordinal)
            .ThenBy(x => x.RelatedAssignmentId).ThenBy(x => x.AvailabilityId).ToArray();
    }

    public static bool Overlaps(DateTimeOffset firstStart, DateTimeOffset firstEnd,
        DateTimeOffset secondStart, DateTimeOffset secondEnd) => firstStart < secondEnd && secondStart < firstEnd;

    private static DateTimeOffset Max(DateTimeOffset first, DateTimeOffset second) => first > second ? first : second;
    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) => first < second ? first : second;
}

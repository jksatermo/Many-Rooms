using ManyRooms.Domain;

namespace ManyRooms.Application;

/// <summary>Deterministic, advisory suggestions; a matching position is a preference, not a qualification.</summary>
public static class ReplacementPolicy
{
    public static IReadOnlyList<ReplacementCandidateDto> Suggest(VolunteerAssignment target,
        IEnumerable<TeamMember> members, IReadOnlyList<VolunteerAssignment> assignments,
        IReadOnlyList<VolunteerUnavailability> unavailable)
    {
        var results = new List<ReplacementCandidateDto>();
        foreach (var member in members.Where(x => x.OrganizationId == target.OrganizationId && x.IsActive &&
            x.TeamId == target.TeamMember.TeamId && x.MembershipId != target.TeamMember.MembershipId))
        {
            var candidate = new VolunteerAssignment { Id = target.Id, OrganizationId = target.OrganizationId,
                EventId = target.EventId, Event = target.Event, TeamMemberId = member.Id, TeamMember = member, RoleName = target.RoleName };
            if (ScheduleConflictDetector.Detect(candidate, assignments, unavailable).Count > 0) continue;
            var own = assignments.Where(x => x.OrganizationId == target.OrganizationId && x.Id != target.Id &&
                !x.IsCancelled && x.TeamMember.MembershipId == member.MembershipId).ToArray();
            if (own.Any(x => x.EventId == target.EventId && x.TeamMember.TeamId == member.TeamId &&
                StaffingPolicy.Normalize(x.RoleName) == StaffingPolicy.Normalize(target.RoleName))) continue;
            var workload = own.Count(x => x.Status != AssignmentStatus.Declined && x.Event.StartsAt >= target.Event.StartsAt &&
                x.Event.StartsAt < target.Event.StartsAt.AddDays(28));
            results.Add(new(member.Id, member.MembershipId, member.Membership.UserId, member.Membership.User.DisplayName,
                member.PositionName, StaffingPolicy.Normalize(member.PositionName) == StaffingPolicy.Normalize(target.RoleName), workload));
        }
        return results.OrderByDescending(x => x.RoleMatch).ThenBy(x => x.UpcomingAssignmentCount)
            .ThenBy(x => x.VolunteerName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.MembershipId).ToArray();
    }
}

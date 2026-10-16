using ManyRooms.Domain;

namespace ManyRooms.Application;

public sealed record TeamSummaryDto(Guid Id, string Name, string? Description, int MemberCount, int UpcomingAssignmentCount);
public sealed record TeamDetailsDto(Guid Id, string Name, string? Description, IReadOnlyList<TeamMemberDto> Members, IReadOnlyList<AssignmentDto> UpcomingAssignments);
public sealed record OrganizationMemberDto(Guid Id, Guid VolunteerId, string VolunteerName);
public sealed record AddTeamMemberRequest(Guid MembershipId, string? PositionName);
public sealed record UpdateTeamMemberRequest(string? PositionName, int ExpectedVersion);
public sealed record StaffingRequirementDto(Guid Id, Guid EventId, Guid TeamId, string TeamName, string? RoleName,
    int DesiredCount, int Version, int Assigned, int Accepted, int Pending, int Declined, int Conflicted, int OpenSpots);
public sealed record CreateStaffingRequest(Guid TeamId, string? RoleName, int DesiredCount);
public sealed record UpdateStaffingRequest(string? RoleName, int DesiredCount, int ExpectedVersion);
public sealed record UpdateAssignmentRequest(Guid TeamMemberId, string? RoleName, int ExpectedVersion);
public sealed record AssignmentHistoryDto(Guid Id, Guid AssignmentId, string Action, DateTimeOffset OccurredAt,
    string? ActorName, string? PreviousVolunteerName, string VolunteerName, string? PreviousRoleName, string RoleName, int AssignmentVersion);
public sealed record ReplacementCandidateDto(Guid TeamMemberId, Guid MembershipId, Guid VolunteerId, string VolunteerName,
    string? PositionName, bool RoleMatch, int UpcomingAssignmentCount);

public interface ILeaderSchedulingStore : ILeaderAssignmentStore
{
    Task<OrganizationMembership?> FindMembershipAsync(Guid organizationId, Guid userId, CancellationToken ct);
    Task<IReadOnlyList<Team>> ListTeamsAsync(Guid organizationId, CancellationToken ct);
    Task<IReadOnlyList<TeamMember>> ListMembersAsync(Guid organizationId, CancellationToken ct);
    Task<IReadOnlyList<OrganizationMembership>> ListMembershipsAsync(Guid organizationId, CancellationToken ct);
    Task<IReadOnlyList<VolunteerAssignment>> ListAssignmentsAsync(Guid organizationId, CancellationToken ct);
    Task<IReadOnlyList<EventStaffingRequirement>> ListStaffingAsync(Guid organizationId, Guid eventId, CancellationToken ct);
    Task<IReadOnlyList<VolunteerUnavailability>> ListUnavailableAsync(Guid organizationId, CancellationToken ct);
    Task<IReadOnlyList<AssignmentHistoryDto>> ListHistoryAsync(Guid organizationId, Guid? eventId, Guid? assignmentId, CancellationToken ct);
    Task SaveSchedulingAsync(OrganizationEntity entity, bool isNew, AssignmentHistory? history, CancellationToken ct);
    Task DeleteStaffingAsync(EventStaffingRequirement entity, CancellationToken ct);
}

/// <summary>Pure staffing policy shared by projections and unit tests.</summary>
public static class StaffingPolicy
{
    public static string Normalize(string? value) => value?.Trim().ToUpperInvariant() ?? "";

    public static StaffingRequirementDto Summarize(EventStaffingRequirement requirement, string teamName,
        IEnumerable<VolunteerAssignment> assignments, IReadOnlySet<Guid> conflictedAssignmentIds)
    {
        var matching = assignments.Where(x => x.OrganizationId == requirement.OrganizationId &&
            x.EventId == requirement.EventId && x.TeamMember.TeamId == requirement.TeamId && !x.IsCancelled &&
            (requirement.NormalizedRoleName.Length == 0 || Normalize(x.RoleName) == requirement.NormalizedRoleName)).ToArray();
        // A person filling two roles within a whole-team requirement still occupies one place.
        int Count(AssignmentStatus status) => matching.Where(x => x.Status == status).Select(x => x.TeamMember.MembershipId).Distinct().Count();
        var assigned = matching.Where(x => x.Status != AssignmentStatus.Declined).Select(x => x.TeamMember.MembershipId).Distinct().Count();
        var conflicted = matching.Where(x => x.Status != AssignmentStatus.Declined && conflictedAssignmentIds.Contains(x.Id))
            .Select(x => x.TeamMember.MembershipId).Distinct().Count();
        return new(requirement.Id, requirement.EventId, requirement.TeamId, teamName, requirement.RoleName,
            requirement.DesiredCount, requirement.Version, assigned, Count(AssignmentStatus.Accepted), Count(AssignmentStatus.Pending),
            Count(AssignmentStatus.Declined), conflicted, Math.Max(0, requirement.DesiredCount - assigned));
    }
}

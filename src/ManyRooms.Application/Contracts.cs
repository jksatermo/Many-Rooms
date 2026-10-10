using ManyRooms.Domain;

namespace ManyRooms.Application;

public sealed record OrganizationDto(Guid Id, string Name, string TimeZone);
public sealed record CurrentUserDto(Guid Id, string DisplayName);
public sealed record DashboardSummaryDto(int UpcomingEvents, int PendingResponses, int AcceptedAssignments);
public sealed record EventSummaryDto(Guid Id, string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    string Location, int AssignmentCount, int PendingResponseCount);
public sealed record AssignmentDto(Guid Id, Guid EventId, string EventTitle, DateTimeOffset EventStartsAt,
    string TeamName, string RoleName, Guid VolunteerId, string VolunteerName, string Status,
    DateTimeOffset? RespondedAt, int Version, bool IsCurrentUser, DateTimeOffset EventEndsAt,
    IReadOnlyList<ScheduleConflictDto> Conflicts);
public sealed record EventDetailsDto(Guid Id, string Title, string Description, DateTimeOffset StartsAt,
    DateTimeOffset EndsAt, string Location, IReadOnlyList<AssignmentDto> Assignments);
public sealed record DashboardDto(OrganizationDto Organization, CurrentUserDto CurrentUser,
    DashboardSummaryDto Summary, IReadOnlyList<EventSummaryDto> Events, IReadOnlyList<AssignmentDto> Assignments);
public sealed record DemoContextDto(Guid OrganizationId, string OrganizationName, Guid UserId, string DisplayName, string TimeZone, string Role);
public sealed record RespondToAssignmentRequest(string? Decision, int ExpectedVersion);
public sealed record ScheduleConflictDto(string Type, string Message, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    Guid? RelatedEventId, string? RelatedEventTitle, Guid? RelatedAssignmentId, Guid? AvailabilityId);
public sealed record UnavailabilityDto(Guid Id, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Note, int Version);
public sealed record CreateUnavailabilityRequest(string? StartsAt, string? EndsAt, string? Note);
public sealed record UpdateUnavailabilityRequest(string? StartsAt, string? EndsAt, string? Note, int ExpectedVersion);
public sealed record TeamMemberDto(Guid Id, Guid TeamId, string TeamName, Guid MembershipId, Guid VolunteerId, string VolunteerName);
public sealed record CreateAssignmentRequest(Guid TeamMemberId, string? RoleName);
public sealed record AssignmentPreviewDto(IReadOnlyList<ScheduleConflictDto> Conflicts);

public interface ICommunityQueries
{
    Task<DemoContextDto?> GetDemoContextAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<bool> IsMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<bool> IsLeaderAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<DashboardDto> GetDashboardAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EventSummaryDto>> GetEventsAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<EventDetailsDto?> GetEventAsync(Guid organizationId, Guid eventId, Guid userId, CancellationToken cancellationToken);
    Task<EventDetailsDto?> GetLeaderEventAsync(Guid organizationId, Guid eventId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TeamMemberDto>> GetTeamMembersAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
}

public interface IAssignmentStore
{
    Task<VolunteerAssignment?> FindAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken);
    Task<bool> IsMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task SaveResponseAsync(VolunteerAssignment assignment, AssignmentResponse response, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScheduleConflictDto>> GetConflictsAsync(VolunteerAssignment assignment, CancellationToken cancellationToken);
}

public interface IAvailabilityStore
{
    Task<OrganizationMembership?> FindMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<VolunteerUnavailability>> ListUnavailabilityAsync(Guid organizationId, Guid membershipId, CancellationToken cancellationToken);
    Task<VolunteerUnavailability?> FindUnavailabilityAsync(Guid organizationId, Guid membershipId, Guid id, CancellationToken cancellationToken);
    Task SaveUnavailabilityAsync(VolunteerUnavailability item, bool isNew, CancellationToken cancellationToken);
    Task DeleteUnavailabilityAsync(VolunteerUnavailability item, CancellationToken cancellationToken);
}

public interface ILeaderAssignmentStore
{
    Task<bool> IsLeaderAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<Event?> FindEventAsync(Guid organizationId, Guid eventId, CancellationToken cancellationToken);
    Task<TeamMember?> FindTeamMemberAsync(Guid organizationId, Guid teamMemberId, CancellationToken cancellationToken);
    Task<bool> AssignmentExistsAsync(Guid organizationId, Guid eventId, Guid teamMemberId, string roleName, CancellationToken cancellationToken);
    Task SaveAssignmentAsync(VolunteerAssignment assignment, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScheduleConflictDto>> GetConflictsAsync(VolunteerAssignment assignment, CancellationToken cancellationToken);
}

public interface ISmsProvider
{
    Task SendAsync(string recipient, string message, CancellationToken cancellationToken);
}

public sealed class AssignmentAccessException : Exception;
public sealed class AssignmentNotFoundException : Exception;
public sealed class SchedulingAccessException : Exception;
public sealed class SchedulingNotFoundException : Exception;

public static class AssignmentMapping
{
    public static AssignmentDto ToDto(this VolunteerAssignment assignment, Guid currentUserId,
        IReadOnlyList<ScheduleConflictDto>? conflicts = null) => new(
        assignment.Id, assignment.EventId, assignment.Event.Title, assignment.Event.StartsAt,
        assignment.TeamMember.Team.Name, assignment.RoleName, assignment.TeamMember.Membership.UserId,
        assignment.TeamMember.Membership.User.DisplayName, assignment.Status.ToString(), assignment.RespondedAt,
        assignment.Version, assignment.TeamMember.Membership.UserId == currentUserId, assignment.Event.EndsAt, conflicts ?? []);
}

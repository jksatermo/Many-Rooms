using ManyRooms.Domain;

namespace ManyRooms.Application;

public sealed record OrganizationDto(Guid Id, string Name, string TimeZone);
public sealed record CurrentUserDto(Guid Id, string DisplayName);
public sealed record DashboardSummaryDto(int UpcomingEvents, int PendingResponses, int AcceptedAssignments);
public sealed record EventSummaryDto(Guid Id, string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    string Location, int AssignmentCount, int PendingResponseCount);
public sealed record AssignmentDto(Guid Id, Guid EventId, string EventTitle, DateTimeOffset EventStartsAt,
    string TeamName, string RoleName, Guid VolunteerId, string VolunteerName, string Status,
    DateTimeOffset? RespondedAt, int Version, bool IsCurrentUser);
public sealed record EventDetailsDto(Guid Id, string Title, string Description, DateTimeOffset StartsAt,
    DateTimeOffset EndsAt, string Location, IReadOnlyList<AssignmentDto> Assignments);
public sealed record DashboardDto(OrganizationDto Organization, CurrentUserDto CurrentUser,
    DashboardSummaryDto Summary, IReadOnlyList<EventSummaryDto> Events, IReadOnlyList<AssignmentDto> Assignments);
public sealed record DemoContextDto(Guid OrganizationId, string OrganizationName, Guid UserId, string DisplayName, string TimeZone);
public sealed record RespondToAssignmentRequest(string? Decision, int ExpectedVersion);

public interface ICommunityQueries
{
    Task<DemoContextDto?> GetDemoContextAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<bool> IsMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<DashboardDto> GetDashboardAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EventSummaryDto>> GetEventsAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<EventDetailsDto?> GetEventAsync(Guid organizationId, Guid eventId, Guid userId, CancellationToken cancellationToken);
}

public interface IAssignmentStore
{
    Task<VolunteerAssignment?> FindAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken);
    Task<bool> IsMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task SaveResponseAsync(VolunteerAssignment assignment, AssignmentResponse response, CancellationToken cancellationToken);
}

public interface ISmsProvider
{
    Task SendAsync(string recipient, string message, CancellationToken cancellationToken);
}

public sealed class AssignmentAccessException : Exception;
public sealed class AssignmentNotFoundException : Exception;

public static class AssignmentMapping
{
    public static AssignmentDto ToDto(this VolunteerAssignment assignment, Guid currentUserId) => new(
        assignment.Id, assignment.EventId, assignment.Event.Title, assignment.Event.StartsAt,
        assignment.TeamMember.Team.Name, assignment.RoleName, assignment.TeamMember.Membership.UserId,
        assignment.TeamMember.Membership.User.DisplayName, assignment.Status.ToString(), assignment.RespondedAt,
        assignment.Version, assignment.TeamMember.Membership.UserId == currentUserId);
}

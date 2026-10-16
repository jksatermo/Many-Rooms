using ManyRooms.Domain;

namespace ManyRooms.Application;

public sealed record EventTemplateDto(Guid Id, string Title, string Description, string Location,
    int DayOfWeek, string LocalStartTime, int DurationMinutes, string StartsOn, string TimeZone);
public sealed record CreateEventTemplateRequest(string? Title, string? Description, string? Location,
    int DayOfWeek, string? LocalStartTime, int DurationMinutes, string? StartsOn);
public sealed record GenerateEventsRequest(string? ThroughDate);
public sealed record GeneratedEventsDto(IReadOnlyList<EventSummaryDto> CreatedEvents, int ExistingCount,
    IReadOnlyList<string> SkippedDates);
public sealed record CreateEventRequest(string? Title, string? Description, string? Location,
    string? StartsAt, string? EndsAt);
public sealed record PlannedOccurrence(DateOnly Date, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
public sealed record RecurrencePlan(IReadOnlyList<PlannedOccurrence> Occurrences, int ExistingCount,
    IReadOnlyList<string> SkippedDates);

public interface IRecurrenceStore
{
    Task<bool> IsLeaderAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);
    Task<Organization?> FindOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EventTemplate>> ListTemplatesAsync(Guid organizationId, CancellationToken cancellationToken);
    Task SaveTemplateAsync(EventTemplate template, CancellationToken cancellationToken);
    Task SaveEventAsync(Event item, CancellationToken cancellationToken);
    // The store serializes each template, then uses RecurrencePlanner inside the transaction.
    Task<GeneratedEventsDto> GenerateAsync(Guid organizationId, Guid templateId, DateOnly throughDate,
        DateTimeOffset now, CancellationToken cancellationToken);
}

using System.Globalization;
using System.Text.RegularExpressions;
using ManyRooms.Domain;

namespace ManyRooms.Application;

public sealed partial class RecurrenceService(IRecurrenceStore store, TimeProvider clock)
{
    public async Task<IReadOnlyList<EventTemplateDto>> ListAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        await RequireLeaderAsync(organizationId, userId, cancellationToken);
        return (await store.ListTemplatesAsync(organizationId, cancellationToken)).Select(ToDto).ToArray();
    }

    public async Task<EventTemplateDto> CreateTemplateAsync(Guid organizationId, Guid userId,
        CreateEventTemplateRequest request, CancellationToken cancellationToken)
    {
        await RequireLeaderAsync(organizationId, userId, cancellationToken);
        var organization = await store.FindOrganizationAsync(organizationId, cancellationToken) ?? throw new SchedulingNotFoundException();
        _ = RecurrencePlanner.FindTimeZone(organization.TimeZone);
        if (request.DayOfWeek is < 0 or > 6) throw new ArgumentException("DayOfWeek must be between 0 (Sunday) and 6 (Saturday).");
        if (request.DurationMinutes is < 15 or > 720) throw new ArgumentException("DurationMinutes must be between 15 and 720.");
        if (!TimeOnly.TryParseExact(request.LocalStartTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var localTime))
            throw new ArgumentException("LocalStartTime must use HH:mm in the organization time zone.");
        var now = clock.GetUtcNow();
        var item = new EventTemplate
        {
            OrganizationId = organizationId,
            Title = Text(request.Title, "Title", 160),
            Description = Description(request.Description),
            Location = Text(request.Location, "Location", 240),
            DayOfWeek = request.DayOfWeek,
            LocalStartTime = localTime,
            DurationMinutes = request.DurationMinutes,
            StartsOn = ParseDate(request.StartsOn),
            TimeZone = organization.TimeZone,
            CreatedAt = now,
            UpdatedAt = now
        };
        await store.SaveTemplateAsync(item, cancellationToken);
        return ToDto(item);
    }

    public async Task<GeneratedEventsDto> GenerateAsync(Guid organizationId, Guid templateId, Guid userId,
        GenerateEventsRequest request, CancellationToken cancellationToken)
    {
        await RequireLeaderAsync(organizationId, userId, cancellationToken);
        return await store.GenerateAsync(organizationId, templateId, ParseDate(request.ThroughDate), clock.GetUtcNow(), cancellationToken);
    }

    public async Task<EventDetailsDto> CreateEventAsync(Guid organizationId, Guid userId,
        CreateEventRequest request, CancellationToken cancellationToken)
    {
        await RequireLeaderAsync(organizationId, userId, cancellationToken);
        var now = clock.GetUtcNow();
        var starts = ParseInstant(request.StartsAt);
        var ends = ParseInstant(request.EndsAt);
        if (ends <= starts) throw new ArgumentException("End must be later than start.");
        if (starts <= now) throw new ArgumentException("The event must start in the future.");
        var item = new Event
        {
            OrganizationId = organizationId,
            Title = Text(request.Title, "Title", 160),
            Description = Description(request.Description),
            Location = Text(request.Location, "Location", 240),
            StartsAt = starts,
            EndsAt = ends,
            CreatedAt = now,
            UpdatedAt = now
        };
        await store.SaveEventAsync(item, cancellationToken);
        return new(item.Id, item.Title, item.Description, item.StartsAt, item.EndsAt, item.Location, []);
    }

    private async Task RequireLeaderAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await store.IsLeaderAsync(organizationId, userId, cancellationToken)) throw new SchedulingAccessException();
    }

    private static EventTemplateDto ToDto(EventTemplate item) => new(item.Id, item.Title, item.Description, item.Location,
        item.DayOfWeek, item.LocalStartTime.ToString("HH:mm", CultureInfo.InvariantCulture), item.DurationMinutes,
        RecurrencePlanner.DateText(item.StartsOn), item.TimeZone);

    private static string Text(string? value, string name, int limit)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > limit)
            throw new ArgumentException($"{name} must contain between 1 and {limit} characters.");
        return trimmed;
    }

    private static string Description(string? value)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length > 4000) throw new ArgumentException("Description must contain at most 4000 characters.");
        return trimmed;
    }

    private static DateOnly ParseDate(string? value) => DateOnly.TryParseExact(value, "yyyy-MM-dd",
        CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : throw new ArgumentException("Use a valid YYYY-MM-DD date.");

    private static DateTimeOffset ParseInstant(string? value)
    {
        if (value is null || !OffsetInstant().IsMatch(value) ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant))
            throw new ArgumentException("Use an ISO 8601 date and time with seconds and an explicit Z or UTC offset.");
        return instant.ToUniversalTime();
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex OffsetInstant();
}

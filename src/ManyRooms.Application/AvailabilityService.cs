using System.Globalization;
using System.Text.RegularExpressions;
using ManyRooms.Domain;

namespace ManyRooms.Application;

public sealed partial class AvailabilityService(IAvailabilityStore store, TimeProvider clock)
{
    public async Task<IReadOnlyList<UnavailabilityDto>> ListAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var membership = await RequireMembershipAsync(organizationId, userId, cancellationToken);
        var entries = await store.ListUnavailabilityAsync(organizationId, membership.Id, cancellationToken);
        return entries.Select(ToDto).ToArray();
    }

    public async Task<UnavailabilityDto> CreateAsync(Guid organizationId, Guid userId,
        CreateUnavailabilityRequest request, CancellationToken cancellationToken)
    {
        var membership = await RequireMembershipAsync(organizationId, userId, cancellationToken);
        var entry = VolunteerUnavailability.Create(organizationId, membership.Id,
            ParseInstant(request.StartsAt), ParseInstant(request.EndsAt), request.Note, clock.GetUtcNow());
        await store.SaveUnavailabilityAsync(entry, true, cancellationToken);
        return ToDto(entry);
    }

    public async Task<UnavailabilityDto> GetAsync(Guid organizationId, Guid id, Guid userId, CancellationToken cancellationToken) =>
        ToDto(await RequireOwnedAsync(organizationId, id, userId, cancellationToken));

    public async Task<UnavailabilityDto> UpdateAsync(Guid organizationId, Guid id, Guid userId,
        UpdateUnavailabilityRequest request, CancellationToken cancellationToken)
    {
        var entry = await RequireOwnedAsync(organizationId, id, userId, cancellationToken);
        entry.Replace(ParseInstant(request.StartsAt), ParseInstant(request.EndsAt), request.Note,
            request.ExpectedVersion, clock.GetUtcNow());
        await store.SaveUnavailabilityAsync(entry, false, cancellationToken);
        return ToDto(entry);
    }

    public async Task DeleteAsync(Guid organizationId, Guid id, Guid userId, int expectedVersion, CancellationToken cancellationToken)
    {
        var entry = await RequireOwnedAsync(organizationId, id, userId, cancellationToken);
        entry.CheckVersion(expectedVersion);
        await store.DeleteUnavailabilityAsync(entry, cancellationToken);
    }

    private async Task<OrganizationMembership> RequireMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        await store.FindMembershipAsync(organizationId, userId, cancellationToken) ?? throw new SchedulingAccessException();

    private async Task<VolunteerUnavailability> RequireOwnedAsync(Guid organizationId, Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var membership = await RequireMembershipAsync(organizationId, userId, cancellationToken);
        return await store.FindUnavailabilityAsync(organizationId, membership.Id, id, cancellationToken)
            ?? throw new SchedulingNotFoundException();
    }

    private static UnavailabilityDto ToDto(VolunteerUnavailability item) => new(item.Id, item.StartsAt, item.EndsAt, item.Note, item.Version);

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

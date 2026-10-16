using ManyRooms.Application;
using ManyRooms.Domain;
using Microsoft.EntityFrameworkCore;

namespace ManyRooms.Infrastructure;

public sealed class RecurrenceStore(ManyRoomsDbContext db) : IRecurrenceStore
{
    public Task<bool> IsLeaderAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        db.Memberships.AnyAsync(x => x.OrganizationId == organizationId && x.UserId == userId && x.Role == MembershipRole.Leader, cancellationToken);

    public Task<Organization?> FindOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        db.Organizations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == organizationId, cancellationToken);

    public async Task<IReadOnlyList<EventTemplate>> ListTemplatesAsync(Guid organizationId, CancellationToken cancellationToken) =>
        await db.Set<EventTemplate>().AsNoTracking().Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.StartsOn).ThenBy(x => x.Title).ThenBy(x => x.Id).ToListAsync(cancellationToken);

    public async Task SaveTemplateAsync(EventTemplate template, CancellationToken cancellationToken)
    {
        db.Add(template);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveEventAsync(Event item, CancellationToken cancellationToken)
    {
        db.Events.Add(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<GeneratedEventsDto> GenerateAsync(Guid organizationId, Guid templateId, DateOnly throughDate,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Serialize concurrent requests for this exact tenant/template. The unique occurrence index
        // is a second database boundary; no cross-organization lock or application-only deduplication.
        var template = await db.Set<EventTemplate>().FromSqlInterpolated(
            $"SELECT * FROM \"EventTemplates\" WHERE \"OrganizationId\" = {organizationId} AND \"Id\" = {templateId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw new SchedulingNotFoundException();
        var startDate = RecurrencePlanner.GenerationStart(template, now);
        var existingDates = await db.Events.Where(x => x.OrganizationId == organizationId &&
                x.TemplateId == templateId && x.OccurrenceDate >= startDate && x.OccurrenceDate <= throughDate)
            .Select(x => x.OccurrenceDate!.Value).ToListAsync(cancellationToken);
        var plan = RecurrencePlanner.Plan(template, throughDate, now, existingDates.ToHashSet());
        var events = plan.Occurrences.Select(occurrence => new Event
        {
            OrganizationId = organizationId,
            TemplateId = template.Id,
            OccurrenceDate = occurrence.Date,
            Title = template.Title,
            Description = template.Description,
            Location = template.Location,
            StartsAt = occurrence.StartsAt,
            EndsAt = occurrence.EndsAt,
            CreatedAt = now,
            UpdatedAt = now
        }).ToArray();
        db.Events.AddRange(events);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(events.Select(item => new EventSummaryDto(item.Id, item.Title, item.StartsAt, item.EndsAt,
            item.Location, 0, 0)).ToArray(), plan.ExistingCount, plan.SkippedDates);
    }
}

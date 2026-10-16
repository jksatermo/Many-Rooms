using ManyRooms.Application;
using ManyRooms.Domain;
using Microsoft.EntityFrameworkCore;

namespace ManyRooms.Infrastructure;

public static class RecurrenceSeed
{
    public static readonly Guid TemplateId = Guid.Parse("90000000-0000-0000-0000-000000000001");

    /// <summary>Called inside DemoData's advisory-locked transaction; never resets generated events.</summary>
    public static async Task SeedAsync(ManyRoomsDbContext db, TimeProvider clock, CancellationToken cancellationToken = default)
    {
        if (await db.Set<EventTemplate>().AnyAsync(x => x.Id == TemplateId, cancellationToken)) return;
        var organization = await db.Organizations.SingleAsync(x => x.Id == DemoData.OrganizationId, cancellationToken);
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, RecurrencePlanner.FindTimeZone(organization.TimeZone)).DateTime);
        db.Add(new EventTemplate
        {
            Id = TemplateId,
            OrganizationId = organization.Id,
            Title = "Weekly neighborhood table",
            Description = "A recurring meal where neighbors make room for one another. Generate the next few weeks, then schedule each gathering individually.",
            Location = "Cedar Grove · Community kitchen",
            DayOfWeek = (int)DayOfWeek.Wednesday,
            LocalStartTime = new TimeOnly(18, 0),
            DurationMinutes = 90,
            StartsOn = today.AddDays(1),
            TimeZone = organization.TimeZone,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}

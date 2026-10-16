using ManyRooms.Domain;
using Microsoft.EntityFrameworkCore;

namespace ManyRooms.Infrastructure;

/// <summary>Fictional, development-only records. Re-running keeps existing responses and dates.</summary>
public static class DemoData
{
    public static readonly Guid OrganizationId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid OtherOrganizationId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid UserId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid LeaderUserId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid FirstEventId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    public static readonly Guid FirstAssignmentId = Guid.Parse("70000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(ManyRoomsDbContext db, TimeProvider clock, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(481729)", cancellationToken);
        if (!await db.Organizations.AnyAsync(x => x.Id == OrganizationId, cancellationToken))
            await SeedInitialAsync(db, clock, cancellationToken);
        await SeedSchedulingAsync(db, clock, cancellationToken);
        await LeaderDemoData.SeedAsync(db, clock, cancellationToken);
        await RecurrenceSeed.SeedAsync(db, clock, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task SeedInitialAsync(ManyRoomsDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var daysUntilSunday = (7 - (int)now.DayOfWeek) % 7;
        if (daysUntilSunday == 0) daysUntilSunday = 7;
        var day = new DateTimeOffset(now.UtcDateTime.Date.AddDays(daysUntilSunday), TimeSpan.Zero).AddHours(15);
        var organizations = new[]
        {
            new Organization { Id = OrganizationId, Name = "Cedar Grove Community", TimeZone = "America/Chicago" },
            new Organization { Id = OtherOrganizationId, Name = "Riverbend Community", TimeZone = "America/Chicago" }
        };
        var users = new[]
        {
            new User { Id = UserId, DisplayName = "Alex Morgan", Email = "alex@example.invalid" },
            new User { Id = Id(2, 2), DisplayName = "Jamie Chen", Email = "jamie@example.invalid" },
            new User { Id = Id(2, 3), DisplayName = "Riley Brooks", Email = "riley@example.invalid" },
            new User { Id = Id(2, 4), DisplayName = "Sam Rivera", Email = "sam@example.invalid" }
        };
        var memberships = users.Select((user, index) => new OrganizationMembership
        {
            Id = Id(3, index + 1),
            OrganizationId = index == 3 ? OtherOrganizationId : OrganizationId,
            UserId = user.Id,
            User = user,
            Role = index == 1 ? MembershipRole.Leader : MembershipRole.Volunteer
        }).ToArray();
        var teams = new[]
        {
            new Team { Id = Id(4, 1), OrganizationId = OrganizationId, Name = "Welcome team", Description = "A friendly face and a place to belong." },
            new Team { Id = Id(4, 2), OrganizationId = OrganizationId, Name = "Table & hospitality", Description = "Make room around the table." },
            new Team { Id = Id(4, 3), OrganizationId = OrganizationId, Name = "Community care", Description = "Show up for our neighbors." },
            new Team { Id = Id(4, 4), OrganizationId = OtherOrganizationId, Name = "Riverbend volunteers" }
        };
        TeamMember Member(int number, int team, int person) => new()
        {
            Id = Id(5, number),
            OrganizationId = teams[team].OrganizationId,
            TeamId = teams[team].Id,
            Team = teams[team],
            MembershipId = memberships[person].Id,
            Membership = memberships[person]
        };
        var members = new[] { Member(1, 0, 0), Member(2, 0, 1), Member(3, 1, 0), Member(4, 1, 2), Member(5, 2, 0), Member(6, 2, 1), Member(7, 3, 3) };
        var events = new[]
        {
            new Event { Id = FirstEventId, OrganizationId = OrganizationId, Title = "Sunday gathering", Description = "Coffee, conversation, and a morning together. Help everyone feel at home, whether this is their first visit or their fiftieth.", Location = "Cedar Grove · Main hall", StartsAt = day, EndsAt = day.AddHours(2) },
            new Event { Id = Id(6, 2), OrganizationId = OrganizationId, Title = "Neighborhood supper", Description = "A shared meal with neighbors and friends. Bring your curiosity; we will make room at the table.", Location = "Cedar Grove · Community kitchen", StartsAt = day.AddDays(4).AddHours(8), EndsAt = day.AddDays(4).AddHours(10) },
            new Event { Id = Id(6, 3), OrganizationId = OrganizationId, Title = "Serve together", Description = "A morning packing pantry boxes and caring for our neighborhood. All experience levels are welcome.", Location = "Cedar Grove · Food pantry", StartsAt = day.AddDays(6).AddHours(-1), EndsAt = day.AddDays(6).AddHours(2) },
            new Event { Id = Id(6, 4), OrganizationId = OtherOrganizationId, Title = "Riverbend community lunch", Description = "A separate fictional organization used to verify isolation.", Location = "Riverbend hall", StartsAt = day.AddDays(2), EndsAt = day.AddDays(2).AddHours(2) }
        };
        VolunteerAssignment Assignment(int number, int eventIndex, int memberIndex, string role, bool accepted = false) => new()
        {
            Id = Id(7, number),
            OrganizationId = events[eventIndex].OrganizationId,
            EventId = events[eventIndex].Id,
            Event = events[eventIndex],
            TeamMemberId = members[memberIndex].Id,
            TeamMember = members[memberIndex],
            RoleName = role,
            Status = accepted ? AssignmentStatus.Accepted : AssignmentStatus.Pending,
            RespondedAt = accepted ? now : null,
            Version = accepted ? 2 : 1
        };
        var assignments = new[]
        {
            Assignment(1, 0, 0, "Front-door welcome"), Assignment(2, 0, 1, "Welcome host", true),
            Assignment(3, 0, 3, "Coffee & connection", true), Assignment(4, 1, 2, "Table host"),
            Assignment(5, 1, 3, "Meal preparation"), Assignment(6, 2, 4, "Pantry packing", true),
            Assignment(7, 2, 5, "Supply coordinator"), Assignment(8, 3, 6, "Lunch host")
        };
        db.AddRange(organizations);
        db.AddRange(users);
        db.AddRange(memberships);
        db.AddRange(teams);
        db.AddRange(members);
        db.AddRange(events);
        db.AddRange(assignments);
        db.AddRange(assignments.Where(x => x.Status == AssignmentStatus.Accepted).Select(x => new AssignmentResponse
        {
            OrganizationId = x.OrganizationId,
            AssignmentId = x.Id,
            MembershipId = x.TeamMember.MembershipId,
            Decision = x.Status,
            AssignmentVersion = x.Version,
            RespondedAt = now
        }));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSchedulingAsync(ManyRoomsDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        // The fixed workshop is an additive upgrade marker. Re-seeding never recreates unavailable
        // times a volunteer deliberately removed, or overwrites any existing response or event date.
        if (await db.Events.AnyAsync(x => x.Id == Id(6, 5), cancellationToken)) return;
        var firstEvent = await db.Events.SingleAsync(x => x.Id == FirstEventId, cancellationToken);
        var now = clock.GetUtcNow();
        var day = firstEvent.StartsAt;
        var workshop = new Event
        {
            Id = Id(6, 5),
            OrganizationId = OrganizationId,
            Title = "Community welcome workshop",
            Description = "A small group practicing ways to make new neighbors feel welcome. This demo intentionally overlaps another commitment so leaders can talk through the schedule.",
            Location = "Cedar Grove · Workshop room",
            StartsAt = day.AddMinutes(30),
            EndsAt = day.AddMinutes(90)
        };
        var garden = new Event
        {
            Id = Id(6, 6),
            OrganizationId = OrganizationId,
            Title = "Neighborhood garden planning",
            Description = "Plan a shared garden with neighbors. This fictional schedule includes an overlap for the scheduling demo.",
            Location = "Cedar Grove · Garden room",
            StartsAt = day.AddMinutes(60),
            EndsAt = day.AddMinutes(120)
        };
        var members = await db.TeamMembers.Where(x => x.OrganizationId == OrganizationId).ToDictionaryAsync(x => x.Id, cancellationToken);
        VolunteerAssignment Assignment(int number, Event item, int memberNumber, string role, AssignmentStatus status) => new()
        {
            Id = Id(7, number),
            OrganizationId = OrganizationId,
            EventId = item.Id,
            Event = item,
            TeamMemberId = Id(5, memberNumber),
            TeamMember = members[Id(5, memberNumber)],
            RoleName = role,
            Status = status,
            Version = status == AssignmentStatus.Pending ? 1 : 2,
            RespondedAt = status == AssignmentStatus.Pending ? null : now,
            CreatedAt = now,
            UpdatedAt = now
        };
        var assignments = new[]
        {
            Assignment(9, workshop, 3, "Practice host", AssignmentStatus.Pending),
            Assignment(10, garden, 5, "Garden planning host", AssignmentStatus.Accepted),
            Assignment(11, workshop, 4, "Refreshments", AssignmentStatus.Declined),
            Assignment(12, garden, 6, "Discussion host", AssignmentStatus.Pending)
        };
        var unavailable = VolunteerUnavailability.Create(OrganizationId, Id(3, 1), day.AddMinutes(45), day.AddMinutes(75),
            "Family appointment", now);
        unavailable.Id = Id(8, 1);
        var leaderUnavailable = VolunteerUnavailability.Create(OrganizationId, Id(3, 2), day, day.AddMinutes(15),
            "Private demo note visible only to Jamie", now);
        leaderUnavailable.Id = Id(8, 2);
        db.Events.AddRange(workshop, garden);
        db.Assignments.AddRange(assignments);
        db.Unavailability.AddRange(unavailable, leaderUnavailable);
        db.Responses.AddRange(assignments.Where(x => x.Status != AssignmentStatus.Pending).Select(x => new AssignmentResponse
        {
            OrganizationId = OrganizationId,
            AssignmentId = x.Id,
            MembershipId = x.TeamMember.MembershipId,
            Decision = x.Status,
            AssignmentVersion = x.Version,
            RespondedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        }));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static Guid Id(int group, int number) => Guid.Parse($"{group}0000000-0000-0000-0000-{number:000000000000}");
}

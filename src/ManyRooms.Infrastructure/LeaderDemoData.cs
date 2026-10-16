using ManyRooms.Domain;
using Microsoft.EntityFrameworkCore;

namespace ManyRooms.Infrastructure;

/// <summary>An additive, one-time M3 seed. Existing schedules and volunteer changes are never reset.</summary>
public static class LeaderDemoData
{
    public static readonly Guid PlanningEventId = Guid.Parse("60000000-0000-0000-0000-000000000020");
    private static Guid Id(int group, int number) => Guid.Parse($"{group}0000000-0000-0000-0000-{number:000000000000}");

    public static async Task SeedAsync(ManyRoomsDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (!await db.Events.AnyAsync(x => x.Id == PlanningEventId, ct))
        {
            var org = DemoData.OrganizationId;
            var now = clock.GetUtcNow();
            var nextSunday = (7 - (int)now.DayOfWeek) % 7;
            if (nextSunday == 0) nextSunday = 7;
            var starts = new DateTimeOffset(now.UtcDateTime.Date.AddDays(nextSunday), TimeSpan.Zero).AddHours(15);
            var users = new[]
            {
                new User { Id = Id(2, 20), DisplayName = "Taylor Lee", Email = "taylor@example.invalid" },
                new User { Id = Id(2, 21), DisplayName = "Casey Patel", Email = "casey@example.invalid" },
                new User { Id = Id(2, 22), DisplayName = "Jordan Ellis", Email = "jordan@example.invalid" }
            };
            var memberships = users.Select((x, i) => new OrganizationMembership { Id = Id(3, 20 + i), OrganizationId = org,
                UserId = x.Id, User = x, Role = MembershipRole.Volunteer }).ToArray();
            var teams = await db.Teams.Where(x => x.OrganizationId == org).ToDictionaryAsync(x => x.Id, ct);
            TeamMember Member(int id, int team, int person, string position) => new()
            {
                Id = Id(5, id), OrganizationId = org, TeamId = Id(4, team), Team = teams[Id(4, team)],
                MembershipId = memberships[person].Id, Membership = memberships[person], PositionName = position
            };
            var members = new[] { Member(20, 1, 0, "Front-door welcome"), Member(21, 1, 1, "Front-door welcome"),
                Member(22, 1, 2, "Welcome host"), Member(23, 2, 0, "Table host"), Member(24, 3, 1, "Pantry packing") };
            var existing = await db.TeamMembers.Where(x => x.OrganizationId == org).Include(x => x.Team)
                .Include(x => x.Membership).ThenInclude(x => x.User).ToDictionaryAsync(x => x.Id, ct);
            foreach (var member in members) existing.Add(member.Id, member);
            Event Event(int number, string title, string description, DateTimeOffset start, DateTimeOffset end) => new()
            {
                Id = Id(6, number), OrganizationId = org, Title = title, Description = description,
                Location = "Cedar Grove · Community hall", StartsAt = start, EndsAt = end, CreatedAt = now, UpdatedAt = now
            };
            var gathering = Event(20, "Welcome Sunday", "Plan a welcoming morning together. This fictional schedule has an open welcome role and a conflict to resolve.", starts, starts.AddHours(2));
            var workshop = Event(21, "Volunteer orientation", "A small-group orientation intentionally overlaps Welcome Sunday to demonstrate replacement planning.", starts.AddMinutes(30), starts.AddMinutes(90));
            var supper = Event(22, "Community table", "A well-staffed evening meal with neighbors and friends.", starts.AddDays(3).AddHours(8), starts.AddDays(3).AddHours(10));
            VolunteerAssignment Assignment(int number, Event item, int member, string role, AssignmentStatus status) => new()
            {
                Id = Id(7, number), OrganizationId = org, EventId = item.Id, Event = item,
                TeamMemberId = Id(5, member), TeamMember = existing[Id(5, member)], RoleName = role,
                Status = status, Version = status == AssignmentStatus.Pending ? 1 : 2,
                RespondedAt = status == AssignmentStatus.Pending ? null : now, CreatedAt = now, UpdatedAt = now
            };
            var assignments = new[]
            {
                Assignment(20, gathering, 1, "Front-door welcome", AssignmentStatus.Pending),
                Assignment(21, gathering, 2, "Welcome host", AssignmentStatus.Accepted),
                Assignment(22, gathering, 4, "Coffee & connection", AssignmentStatus.Declined),
                Assignment(23, workshop, 3, "Orientation host", AssignmentStatus.Accepted),
                Assignment(24, supper, 23, "Table host", AssignmentStatus.Accepted),
                Assignment(25, supper, 4, "Meal preparation", AssignmentStatus.Accepted)
            };
            var unavailable = VolunteerUnavailability.Create(org, Id(3, 1), starts.AddMinutes(45), starts.AddMinutes(75), "Family visit", now);
            unavailable.Id = Id(8, 20);
            EventStaffingRequirement Requirement(int number, Event item, int team, string? role, int desired) => new()
            {
                Id = Id(9, number), OrganizationId = org, EventId = item.Id, TeamId = Id(4, team), RoleName = role,
                NormalizedRoleName = Application.StaffingPolicy.Normalize(role), DesiredCount = desired, CreatedAt = now, UpdatedAt = now
            };
            db.AddRange(users);
            db.AddRange(memberships);
            db.AddRange(members);
            db.Events.AddRange(gathering, workshop, supper);
            db.Assignments.AddRange(assignments);
            db.Unavailability.Add(unavailable);
            db.AddRange(Requirement(20, gathering, 1, "Front-door welcome", 2), Requirement(21, gathering, 1, "Welcome host", 1),
                Requirement(22, gathering, 2, null, 2), Requirement(23, supper, 2, null, 2));
            foreach (var assignment in assignments)
            {
                var created = AssignmentHistory.Capture(assignment, Id(3, 2), "Created", now);
                created.AssignmentVersion = 1;
                db.Add(created);
                if (assignment.Status != AssignmentStatus.Pending)
                {
                    db.Responses.Add(new AssignmentResponse { OrganizationId = org, AssignmentId = assignment.Id,
                        MembershipId = assignment.TeamMember.MembershipId, Decision = assignment.Status, AssignmentVersion = 2,
                        RespondedAt = now, CreatedAt = now, UpdatedAt = now });
                    db.Add(AssignmentHistory.Capture(assignment, assignment.TeamMember.MembershipId, assignment.Status.ToString(), now));
                }
            }
            await db.SaveChangesAsync(ct);
        }
        // A clean database applies migrations before seeding M1/M2. Supply the same explicitly
        // imported legacy history as the upgrade migration; never replace a stored history row.
        var legacy = await db.Assignments.Include(x => x.TeamMember).ThenInclude(x => x.Membership).ThenInclude(x => x.User)
            .Where(x => !db.Set<AssignmentHistory>().Any(h => h.OrganizationId == x.OrganizationId && h.AssignmentId == x.Id)).ToListAsync(ct);
        foreach (var assignment in legacy)
        {
            var imported = AssignmentHistory.Capture(assignment, null, "Imported", assignment.CreatedAt);
            imported.AssignmentVersion = 1;
            db.Add(imported);
            var responses = await db.Responses.Where(x => x.OrganizationId == assignment.OrganizationId && x.AssignmentId == assignment.Id).ToListAsync(ct);
            foreach (var response in responses)
            {
                var history = AssignmentHistory.Capture(assignment, response.MembershipId, response.Decision.ToString(), response.RespondedAt);
                history.AssignmentVersion = response.AssignmentVersion;
                db.Add(history);
            }
        }
        await db.SaveChangesAsync(ct);
    }
}

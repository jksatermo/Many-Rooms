using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ManyRooms.Domain;
using ManyRooms.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ManyRooms.Api.Tests;

internal static class SchedulingTestData
{
    public static string OrganizationPath => $"/api/organizations/{DemoData.OrganizationId}";
    public static string LeaderPath => $"{OrganizationPath}/leader";
    public static DateTimeOffset Future => new(DateTime.UtcNow.Date.AddDays(70).AddHours(15), TimeSpan.Zero);

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == expected, $"Expected {expected}, received {response.StatusCode}: {body}");
            return JsonSerializer.Deserialize<JsonElement>(body);
        }
    }

    public static async Task<Team> TeamAsync(PostgresFixture fixture)
    {
        await using var db = fixture.CreateDbContext();
        var team = new Team { OrganizationId = DemoData.OrganizationId, Name = $"Test team {Guid.NewGuid():N}", Description = "Fictional scheduling tests" };
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team;
    }

    public static async Task<TeamMember> MemberAsync(PostgresFixture fixture, Team team, string name = "Fictional volunteer",
        string? position = null, Guid? existingUserId = null, bool active = true)
    {
        await using var db = fixture.CreateDbContext();
        OrganizationMembership membership;
        if (existingUserId is { } userId)
            membership = await db.Memberships.Include(x => x.User).SingleAsync(x => x.OrganizationId == team.OrganizationId && x.UserId == userId);
        else
        {
            var user = new User { DisplayName = name, Email = $"{Guid.NewGuid():N}@example.invalid" };
            membership = new OrganizationMembership { OrganizationId = team.OrganizationId, UserId = user.Id, User = user };
            db.Memberships.Add(membership);
        }
        var member = new TeamMember
        {
            OrganizationId = team.OrganizationId, TeamId = team.Id, MembershipId = membership.Id, Membership = membership,
            PositionName = position, IsActive = active
        };
        db.TeamMembers.Add(member);
        await db.SaveChangesAsync();
        return member;
    }

    public static async Task<Event> EventAsync(PostgresFixture fixture, DateTimeOffset? startsAt = null, DateTimeOffset? endsAt = null)
    {
        await using var db = fixture.CreateDbContext();
        var item = new Event
        {
            OrganizationId = DemoData.OrganizationId, Title = $"Test gathering {Guid.NewGuid():N}", Description = "Fictional event",
            Location = "Community room", StartsAt = startsAt ?? Future, EndsAt = endsAt ?? (startsAt ?? Future).AddHours(2)
        };
        db.Events.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public static async Task<VolunteerAssignment> AssignmentAsync(PostgresFixture fixture, Event item, TeamMember member,
        string role = "Host", AssignmentStatus status = AssignmentStatus.Pending, bool cancelled = false)
    {
        await using var db = fixture.CreateDbContext();
        var assignment = new VolunteerAssignment
        {
            OrganizationId = item.OrganizationId, EventId = item.Id, TeamMemberId = member.Id, RoleName = role,
            Status = status, RespondedAt = status == AssignmentStatus.Pending ? null : DateTimeOffset.UtcNow,
            Version = status == AssignmentStatus.Pending ? 1 : 2, IsCancelled = cancelled,
            CancelledAt = cancelled ? DateTimeOffset.UtcNow : null
        };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();
        return assignment;
    }

    public static async Task<VolunteerAssignment> CreateThroughApiAsync(HttpClient leader, Event item, TeamMember member, string role = "Host")
    {
        var body = await JsonAsync(await leader.PostAsJsonAsync($"{LeaderPath}/events/{item.Id}/assignments",
            new { teamMemberId = member.Id, roleName = role }), HttpStatusCode.Created);
        return new VolunteerAssignment
        {
            Id = body.GetProperty("id").GetGuid(), OrganizationId = item.OrganizationId, EventId = item.Id,
            TeamMemberId = member.Id, RoleName = body.GetProperty("roleName").GetString()!, Version = body.GetProperty("version").GetInt32()
        };
    }
}

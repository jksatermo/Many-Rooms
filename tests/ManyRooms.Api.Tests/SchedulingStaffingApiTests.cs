using System.Net;
using System.Net.Http.Json;
using ManyRooms.Domain;
using ManyRooms.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static ManyRooms.Api.Tests.SchedulingTestData;

namespace ManyRooms.Api.Tests;

public sealed class SchedulingStaffingApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Staffing_counts_distinct_active_members_and_exposes_open_spots_statuses_and_conflicts()
    {
        var team = await TeamAsync(fixture);
        var item = await EventAsync(fixture);
        var pending = await MemberAsync(fixture, team);
        var accepted = await MemberAsync(fixture, team);
        var declined = await MemberAsync(fixture, team);
        var cancelled = await MemberAsync(fixture, team);
        await AssignmentAsync(fixture, item, pending, "Host");
        await AssignmentAsync(fixture, item, pending, "Setup"); // One volunteer fills one whole-team spot.
        await AssignmentAsync(fixture, item, accepted, status: AssignmentStatus.Accepted);
        await AssignmentAsync(fixture, item, declined, status: AssignmentStatus.Declined);
        await AssignmentAsync(fixture, item, cancelled, cancelled: true);
        await using (var db = fixture.CreateDbContext())
        {
            db.Unavailability.Add(VolunteerUnavailability.Create(item.OrganizationId, pending.MembershipId,
                item.StartsAt, item.EndsAt, "Private reason not exposed to leaders", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        using var leader = fixture.CreateLeaderClient();
        var path = $"{LeaderPath}/events/{item.Id}/staffing";
        var created = await JsonAsync(await leader.PostAsJsonAsync(path, new { teamId = team.Id, roleName = (string?)null, desiredCount = 4 }), HttpStatusCode.Created);
        AssertCounts(created, assigned: 2, accepted: 1, pending: 1, declined: 1, conflicted: 1, open: 2);
        var refreshed = await JsonAsync(await leader.GetAsync(path));
        AssertCounts(Assert.Single(refreshed.EnumerateArray()), 2, 1, 1, 1, 1, 2);
        var id = created.GetProperty("id").GetGuid();
        var reduced = await JsonAsync(await leader.PutAsJsonAsync($"{path}/{id}", new { roleName = (string?)null, desiredCount = 1, expectedVersion = 1 }));
        Assert.Equal(0, reduced.GetProperty("openSpots").GetInt32()); // Overstaffing is advisory.
        Assert.Equal(2, reduced.GetProperty("version").GetInt32());
        using var stale = await leader.DeleteAsync($"{path}/{id}?expectedVersion=1");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var deleted = await leader.DeleteAsync($"{path}/{id}?expectedVersion=2");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await using var after = fixture.CreateDbContext();
        Assert.Equal(5, await after.Assignments.CountAsync(x => x.EventId == item.Id));
    }

    [Fact]
    public async Task Named_roles_match_trimmed_case_insensitive_values_and_do_not_count_other_roles_or_teams()
    {
        var team = await TeamAsync(fixture);
        var otherTeam = await TeamAsync(fixture);
        var item = await EventAsync(fixture);
        await AssignmentAsync(fixture, item, await MemberAsync(fixture, team), "  hOsT  ");
        await AssignmentAsync(fixture, item, await MemberAsync(fixture, team), "Setup");
        await AssignmentAsync(fixture, item, await MemberAsync(fixture, otherTeam), "Host");
        using var leader = fixture.CreateLeaderClient();
        var path = $"{LeaderPath}/events/{item.Id}/staffing";
        var created = await JsonAsync(await leader.PostAsJsonAsync(path, new { teamId = team.Id, roleName = " Host ", desiredCount = 2 }), HttpStatusCode.Created);
        Assert.Equal("Host", created.GetProperty("roleName").GetString());
        Assert.Equal(1, created.GetProperty("assigned").GetInt32());
        Assert.Equal(1, created.GetProperty("openSpots").GetInt32());
        using var duplicate = await leader.PostAsJsonAsync(path, new { teamId = team.Id, roleName = "hOST", desiredCount = 2 });
        using var wholeTeam = await leader.PostAsJsonAsync(path, new { teamId = team.Id, roleName = (string?)null, desiredCount = 2 });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, wholeTeam.StatusCode);
    }

    [Fact]
    public async Task Concurrent_whole_team_and_named_requirements_cannot_create_ambiguous_counts()
    {
        var team = await TeamAsync(fixture);
        var item = await EventAsync(fixture);
        using var one = fixture.CreateLeaderClient();
        using var two = fixture.CreateLeaderClient();
        var path = $"{LeaderPath}/events/{item.Id}/staffing";
        var results = await Task.WhenAll(one.PostAsJsonAsync(path, new { teamId = team.Id, roleName = (string?)null, desiredCount = 2 }),
            two.PostAsJsonAsync(path, new { teamId = team.Id, roleName = "Host", desiredCount = 2 }));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        foreach (var result in results) result.Dispose();
        Assert.Single((await JsonAsync(await one.GetAsync(path))).EnumerateArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Required_count_is_bounded(int desiredCount)
    {
        var team = await TeamAsync(fixture);
        var item = await EventAsync(fixture);
        using var leader = fixture.CreateLeaderClient();
        using var response = await leader.PostAsJsonAsync($"{LeaderPath}/events/{item.Id}/staffing", new { teamId = team.Id, roleName = "Host", desiredCount });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Staffing_rejects_foreign_organization_references_and_volunteer_access()
    {
        var team = await TeamAsync(fixture);
        var item = await EventAsync(fixture);
        await using var db = fixture.CreateDbContext();
        var foreignTeam = await db.Teams.SingleAsync(x => x.OrganizationId == DemoData.OtherOrganizationId);
        var foreignEvent = await db.Events.SingleAsync(x => x.OrganizationId == DemoData.OtherOrganizationId);
        using var leader = fixture.CreateLeaderClient();
        using var volunteer = fixture.CreateClient();
        var path = $"{LeaderPath}/events/{item.Id}/staffing";
        using var forbidden = await volunteer.GetAsync(path);
        using var forbiddenWrite = await volunteer.PostAsJsonAsync(path, new { teamId = team.Id, roleName = "Host", desiredCount = 2 });
        using var foreignRead = await leader.GetAsync($"{LeaderPath}/events/{foreignEvent.Id}/staffing");
        using var foreignWrite = await leader.PostAsJsonAsync(path, new { teamId = foreignTeam.Id, roleName = "Host", desiredCount = 2 });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenWrite.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignRead.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignWrite.StatusCode);
    }

    private static void AssertCounts(System.Text.Json.JsonElement dto, int assigned, int accepted, int pending, int declined, int conflicted, int open)
    {
        Assert.Equal(assigned, dto.GetProperty("assigned").GetInt32());
        Assert.Equal(accepted, dto.GetProperty("accepted").GetInt32());
        Assert.Equal(pending, dto.GetProperty("pending").GetInt32());
        Assert.Equal(declined, dto.GetProperty("declined").GetInt32());
        Assert.Equal(conflicted, dto.GetProperty("conflicted").GetInt32());
        Assert.Equal(open, dto.GetProperty("openSpots").GetInt32());
    }
}

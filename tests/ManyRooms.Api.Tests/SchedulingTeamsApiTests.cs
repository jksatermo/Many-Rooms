using System.Net;
using System.Net.Http.Json;
using ManyRooms.Domain;
using ManyRooms.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static ManyRooms.Api.Tests.SchedulingTestData;

namespace ManyRooms.Api.Tests;

public sealed class SchedulingTeamsApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Team_membership_supports_trimmed_positions_versions_safe_removal_and_reactivation()
    {
        var team = await TeamAsync(fixture);
        await using var db = fixture.CreateDbContext();
        var membership = await db.Memberships.SingleAsync(x => x.OrganizationId == DemoData.OrganizationId && x.UserId == DemoData.UserId);
        using var leader = fixture.CreateLeaderClient();
        var path = $"{LeaderPath}/teams/{team.Id}/members";
        var created = await JsonAsync(await leader.PostAsJsonAsync(path, new { membershipId = membership.Id, positionName = "  Welcome host  " }), HttpStatusCode.Created);
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Welcome host", created.GetProperty("positionName").GetString());
        Assert.Equal(1, created.GetProperty("version").GetInt32());
        using var duplicate = await leader.PostAsJsonAsync(path, new { membershipId = membership.Id, positionName = "Host" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var updated = await JsonAsync(await leader.PutAsJsonAsync($"{path}/{id}", new { positionName = "  Setup helper  ", expectedVersion = 1 }));
        Assert.Equal("Setup helper", updated.GetProperty("positionName").GetString());
        Assert.Equal(2, updated.GetProperty("version").GetInt32());
        using var staleDelete = await leader.DeleteAsync($"{path}/{id}?expectedVersion=1");
        Assert.Equal(HttpStatusCode.Conflict, staleDelete.StatusCode);
        using var removed = await leader.DeleteAsync($"{path}/{id}?expectedVersion=2");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        var stored = await db.TeamMembers.SingleAsync(x => x.Id == id);
        Assert.False(stored.IsActive);
        Assert.Equal(3, stored.Version);
        var active = await JsonAsync(await leader.GetAsync($"{LeaderPath}/team-members"));
        Assert.DoesNotContain(active.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        var readded = await JsonAsync(await leader.PostAsJsonAsync(path, new { membershipId = membership.Id, positionName = " " }), HttpStatusCode.Created);
        Assert.Equal(id, readded.GetProperty("id").GetGuid());
        Assert.Equal(4, readded.GetProperty("version").GetInt32());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, readded.GetProperty("positionName").ValueKind);
        await db.Entry(stored).ReloadAsync();
        Assert.True(stored.IsActive);
        var detail = await JsonAsync(await leader.GetAsync($"{LeaderPath}/teams/{team.Id}"));
        Assert.Single(detail.GetProperty("members").EnumerateArray());
    }

    [Theory]
    [InlineData(AssignmentStatus.Pending, false, false, HttpStatusCode.Conflict)]
    [InlineData(AssignmentStatus.Accepted, false, false, HttpStatusCode.Conflict)]
    [InlineData(AssignmentStatus.Declined, false, false, HttpStatusCode.NoContent)]
    [InlineData(AssignmentStatus.Pending, true, false, HttpStatusCode.NoContent)]
    [InlineData(AssignmentStatus.Pending, false, true, HttpStatusCode.NoContent)]
    public async Task Removal_only_blocks_future_active_commitments(AssignmentStatus status, bool cancelled, bool past, HttpStatusCode expected)
    {
        var team = await TeamAsync(fixture);
        var member = await MemberAsync(fixture, team);
        var item = await EventAsync(fixture, past ? DateTimeOffset.UtcNow.AddDays(-1) : Future);
        var assignment = await AssignmentAsync(fixture, item, member, status: status, cancelled: cancelled);
        using var leader = fixture.CreateLeaderClient();
        using var result = await leader.DeleteAsync($"{LeaderPath}/teams/{team.Id}/members/{member.Id}?expectedVersion=1");
        Assert.Equal(expected, result.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.Equal(expected == HttpStatusCode.Conflict, (await db.TeamMembers.SingleAsync(x => x.Id == member.Id)).IsActive);
        Assert.True(await db.Assignments.AnyAsync(x => x.Id == assignment.Id));
    }

    [Fact]
    public async Task Concurrent_position_edits_have_one_winner_and_preserve_version()
    {
        var team = await TeamAsync(fixture);
        var member = await MemberAsync(fixture, team);
        using var one = fixture.CreateLeaderClient();
        using var two = fixture.CreateLeaderClient();
        var path = $"{LeaderPath}/teams/{team.Id}/members/{member.Id}";
        var results = await Task.WhenAll(one.PutAsJsonAsync(path, new { positionName = "Host", expectedVersion = 1 }),
            two.PutAsJsonAsync(path, new { positionName = "Setup", expectedVersion = 1 }));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        foreach (var result in results) result.Dispose();
        await using var db = fixture.CreateDbContext();
        Assert.Equal(2, (await db.TeamMembers.SingleAsync(x => x.Id == member.Id)).Version);
    }

    [Fact]
    public async Task Concurrent_remove_and_assignment_creation_cannot_leave_an_inactive_assigned_member()
    {
        var team = await TeamAsync(fixture);
        var member = await MemberAsync(fixture, team);
        var item = await EventAsync(fixture);
        using var one = fixture.CreateLeaderClient();
        using var two = fixture.CreateLeaderClient();
        var results = await Task.WhenAll(one.DeleteAsync($"{LeaderPath}/teams/{team.Id}/members/{member.Id}?expectedVersion=1"),
            two.PostAsJsonAsync($"{LeaderPath}/events/{item.Id}/assignments", new { teamMemberId = member.Id, roleName = "Host" }));
        Assert.DoesNotContain(results, x => (int)x.StatusCode >= 500);
        await using var db = fixture.CreateDbContext();
        var storedMember = await db.TeamMembers.SingleAsync(x => x.Id == member.Id);
        var assigned = await db.Assignments.AnyAsync(x => x.EventId == item.Id && x.TeamMemberId == member.Id);
        Assert.True(storedMember.IsActive || !assigned, "A successful removal must prevent a concurrent assignment from using that member.");
        Assert.True(results.Any(x => x.IsSuccessStatusCode));
        Assert.False(results.All(x => x.IsSuccessStatusCode));
        foreach (var result in results) result.Dispose();
    }

    [Fact]
    public async Task Team_and_membership_ids_are_organization_scoped_and_volunteers_cannot_manage_members()
    {
        var team = await TeamAsync(fixture);
        var member = await MemberAsync(fixture, team);
        await using var db = fixture.CreateDbContext();
        var foreignMember = await db.TeamMembers.SingleAsync(x => x.OrganizationId == DemoData.OtherOrganizationId);
        using var leader = fixture.CreateLeaderClient();
        using var volunteer = fixture.CreateClient();
        using var forbidden = await volunteer.GetAsync($"{LeaderPath}/teams");
        using var forbiddenWrite = await volunteer.PutAsJsonAsync($"{LeaderPath}/teams/{team.Id}/members/{member.Id}", new { positionName = "Host", expectedVersion = 1 });
        using var foreignRead = await leader.GetAsync($"{LeaderPath}/teams/{foreignMember.TeamId}");
        using var foreignAdd = await leader.PostAsJsonAsync($"{LeaderPath}/teams/{team.Id}/members", new { membershipId = foreignMember.MembershipId, positionName = "Host" });
        using var foreignEdit = await leader.DeleteAsync($"{LeaderPath}/teams/{team.Id}/members/{foreignMember.Id}?expectedVersion=1");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenWrite.StatusCode);
        Assert.All(new[] { foreignRead, foreignAdd, foreignEdit }, x => Assert.Equal(HttpStatusCode.NotFound, x.StatusCode));
        var members = await JsonAsync(await leader.GetAsync($"{LeaderPath}/organization-members"));
        Assert.DoesNotContain(members.EnumerateArray(), x => x.GetProperty("id").GetGuid() == foreignMember.MembershipId);
    }

    [Fact]
    public async Task Position_limits_and_positive_versions_are_validated_without_writes()
    {
        var team = await TeamAsync(fixture);
        var member = await MemberAsync(fixture, team);
        using var leader = fixture.CreateLeaderClient();
        var path = $"{LeaderPath}/teams/{team.Id}/members/{member.Id}";
        using var tooLong = await leader.PutAsJsonAsync(path, new { positionName = new string('x', 121), expectedVersion = 1 });
        using var invalidVersion = await leader.PutAsJsonAsync(path, new { positionName = "Host", expectedVersion = 0 });
        using var delete = await leader.DeleteAsync($"{path}?expectedVersion=0");
        Assert.All(new[] { tooLong, invalidVersion, delete }, x => Assert.Equal(HttpStatusCode.BadRequest, x.StatusCode));
        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, (await db.TeamMembers.SingleAsync(x => x.Id == member.Id)).Version);
    }
}

using System.Net;
using System.Net.Http.Json;
using ManyRooms.Domain;
using ManyRooms.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static ManyRooms.Api.Tests.SchedulingTestData;

namespace ManyRooms.Api.Tests;

public sealed class SchedulingAssignmentsApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Reassignment_resets_response_preserves_original_response_and_records_actor_and_snapshots()
    {
        var team = await TeamAsync(fixture);
        var alex = await MemberAsync(fixture, team, existingUserId: DemoData.UserId);
        var jamie = await MemberAsync(fixture, team, existingUserId: DemoData.LeaderUserId);
        var item = await EventAsync(fixture);
        using var leader = fixture.CreateLeaderClient();
        using var volunteer = fixture.CreateClient();
        var assignment = await CreateThroughApiAsync(leader, item, alex);
        var path = $"{LeaderPath}/assignments/{assignment.Id}";
        await JsonAsync(await volunteer.PutAsJsonAsync($"{OrganizationPath}/assignments/{assignment.Id}/response", new { decision = "Accepted", expectedVersion = 1 }));
        var preview = await JsonAsync(await leader.PostAsJsonAsync($"{path}/preview", new { teamMemberId = jamie.Id, roleName = "  Setup helper  ", expectedVersion = 2 }));
        Assert.Empty(preview.GetProperty("conflicts").EnumerateArray());
        await using (var before = fixture.CreateDbContext())
        {
            var stored = await before.Assignments.SingleAsync(x => x.Id == assignment.Id);
            Assert.Equal(alex.Id, stored.TeamMemberId);
            Assert.Equal(AssignmentStatus.Accepted, stored.Status);
            Assert.Equal(2, stored.Version);
        }
        var changed = await JsonAsync(await leader.PutAsJsonAsync(path, new { teamMemberId = jamie.Id, roleName = "  Setup helper  ", expectedVersion = 2 }));
        Assert.Equal(jamie.Id, changed.GetProperty("teamMemberId").GetGuid());
        Assert.Equal("Pending", changed.GetProperty("status").GetString());
        Assert.Equal("Setup helper", changed.GetProperty("roleName").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, changed.GetProperty("respondedAt").ValueKind);
        Assert.Equal(3, changed.GetProperty("version").GetInt32());
        using var formerOwner = await volunteer.PutAsJsonAsync($"{OrganizationPath}/assignments/{assignment.Id}/response", new { decision = "Declined", expectedVersion = 3 });
        Assert.Equal(HttpStatusCode.Forbidden, formerOwner.StatusCode);
        await JsonAsync(await leader.PutAsJsonAsync($"{OrganizationPath}/assignments/{assignment.Id}/response", new { decision = "Accepted", expectedVersion = 3 }));
        var history = (await JsonAsync(await leader.GetAsync($"{path}/history"))).EnumerateArray().ToArray();
        Assert.Equal(new[] { "Accepted", "Reassigned", "Accepted", "Created" }, history.Select(x => x.GetProperty("action").GetString()));
        Assert.Equal(new[] { 4, 3, 2, 1 }, history.Select(x => x.GetProperty("assignmentVersion").GetInt32()));
        Assert.Equal("Alex Morgan", history[1].GetProperty("previousVolunteerName").GetString());
        Assert.Equal("Jamie Chen", history[1].GetProperty("volunteerName").GetString());
        Assert.Equal("Jamie Chen", history[1].GetProperty("actorName").GetString());
        Assert.Equal("Host", history[1].GetProperty("previousRoleName").GetString());
        Assert.Equal("Setup helper", history[1].GetProperty("roleName").GetString());
        Assert.Equal("Alex Morgan", history[2].GetProperty("actorName").GetString());
        await using var db = fixture.CreateDbContext();
        var responses = await db.Responses.Where(x => x.AssignmentId == assignment.Id).OrderBy(x => x.AssignmentVersion).ToListAsync();
        Assert.Equal(2, responses.Count);
        Assert.Equal(alex.MembershipId, responses[0].MembershipId);
        Assert.Equal(2, responses[0].AssignmentVersion);
        Assert.Equal(jamie.MembershipId, responses[1].MembershipId);
    }

    [Fact]
    public async Task Role_change_resets_confirmation_and_cancellation_is_visible_but_unresponsive_and_nonblocking()
    {
        var team = await TeamAsync(fixture);
        var member = await MemberAsync(fixture, team, existingUserId: DemoData.UserId);
        var item = await EventAsync(fixture);
        using var leader = fixture.CreateLeaderClient();
        using var volunteer = fixture.CreateClient();
        var assignment = await CreateThroughApiAsync(leader, item, member);
        var path = $"{LeaderPath}/assignments/{assignment.Id}";
        await JsonAsync(await volunteer.PutAsJsonAsync($"{OrganizationPath}/assignments/{assignment.Id}/response", new { decision = "Accepted", expectedVersion = 1 }));
        var roleChanged = await JsonAsync(await leader.PutAsJsonAsync(path, new { teamMemberId = member.Id, roleName = "Setup", expectedVersion = 2 }));
        Assert.Equal("Pending", roleChanged.GetProperty("status").GetString());
        var cancelled = await JsonAsync(await leader.DeleteAsync($"{path}?expectedVersion=3"));
        Assert.True(cancelled.GetProperty("isCancelled").GetBoolean());
        Assert.Equal(4, cancelled.GetProperty("version").GetInt32());
        Assert.Empty(cancelled.GetProperty("conflicts").EnumerateArray());
        using var response = await volunteer.PutAsJsonAsync($"{OrganizationPath}/assignments/{assignment.Id}/response", new { decision = "Accepted", expectedVersion = 4 });
        using var edit = await leader.PutAsJsonAsync(path, new { teamMemberId = member.Id, roleName = "Host", expectedVersion = 4 });
        using var repeat = await leader.DeleteAsync($"{path}?expectedVersion=4");
        Assert.All(new[] { response, edit, repeat }, x => Assert.Equal(HttpStatusCode.Conflict, x.StatusCode));
        var eventDetail = await JsonAsync(await volunteer.GetAsync($"{OrganizationPath}/events/{item.Id}"));
        Assert.True(Assert.Single(eventDetail.GetProperty("assignments").EnumerateArray()).GetProperty("isCancelled").GetBoolean());
        var history = (await JsonAsync(await leader.GetAsync($"{path}/history"))).EnumerateArray().ToArray();
        Assert.Equal(new[] { "Cancelled", "RoleChanged", "Accepted", "Created" }, history.Select(x => x.GetProperty("action").GetString()));
        var recreated = await CreateThroughApiAsync(leader, item, member, "Setup");
        Assert.NotEqual(assignment.Id, recreated.Id); // Cancelled rows do not block reusing the slot.
        await using var db = fixture.CreateDbContext();
        Assert.NotNull((await db.Assignments.SingleAsync(x => x.Id == assignment.Id)).CancelledAt);
        Assert.Single(await db.Responses.Where(x => x.AssignmentId == assignment.Id).ToListAsync());
    }

    [Fact]
    public async Task Concurrent_leader_edits_have_one_winner_and_exactly_one_history_entry_for_the_new_version()
    {
        var team = await TeamAsync(fixture);
        var member = await MemberAsync(fixture, team);
        var item = await EventAsync(fixture);
        using var one = fixture.CreateLeaderClient();
        using var two = fixture.CreateLeaderClient();
        var assignment = await CreateThroughApiAsync(one, item, member);
        var path = $"{LeaderPath}/assignments/{assignment.Id}";
        var results = await Task.WhenAll(one.PutAsJsonAsync(path, new { teamMemberId = member.Id, roleName = "Setup", expectedVersion = 1 }),
            two.DeleteAsync($"{path}?expectedVersion=1"));
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);
        foreach (var result in results) result.Dispose();
        var history = (await JsonAsync(await one.GetAsync($"{path}/history"))).EnumerateArray().ToArray();
        Assert.Equal(new[] { 2, 1 }, history.Select(x => x.GetProperty("assignmentVersion").GetInt32()));
    }

    [Fact]
    public async Task Invalid_or_foreign_replacements_and_stale_requests_leave_assignment_and_history_unchanged()
    {
        var team = await TeamAsync(fixture);
        var member = await MemberAsync(fixture, team);
        var inactive = await MemberAsync(fixture, team, active: false);
        var wrongTeam = await MemberAsync(fixture, await TeamAsync(fixture));
        var item = await EventAsync(fixture);
        using var leader = fixture.CreateLeaderClient();
        var assignment = await CreateThroughApiAsync(leader, item, member);
        var path = $"{LeaderPath}/assignments/{assignment.Id}";
        await using var db = fixture.CreateDbContext();
        var foreign = await db.TeamMembers.SingleAsync(x => x.OrganizationId == DemoData.OtherOrganizationId);
        foreach (var target in new[] { inactive.Id, wrongTeam.Id, foreign.Id })
        {
            using var result = await leader.PutAsJsonAsync(path, new { teamMemberId = target, roleName = "Host", expectedVersion = 1 });
            Assert.True(result.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
        }
        using var stale = await leader.PutAsJsonAsync(path, new { teamMemberId = member.Id, roleName = "Setup", expectedVersion = 2 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var version = await leader.DeleteAsync($"{path}?expectedVersion=0");
        Assert.Equal(HttpStatusCode.BadRequest, version.StatusCode);
        Assert.Equal(1, (await db.Assignments.SingleAsync(x => x.Id == assignment.Id)).Version);
        Assert.Single((await JsonAsync(await leader.GetAsync($"{path}/history"))).EnumerateArray());
    }

    [Fact]
    public async Task Leader_mutation_history_and_conflict_routes_enforce_role_and_organization_scope()
    {
        var assignment = await fixture.CreateAssignmentAsync();
        await using var db = fixture.CreateDbContext();
        var foreign = await db.Assignments.SingleAsync(x => x.OrganizationId == DemoData.OtherOrganizationId);
        using var volunteer = fixture.CreateClient();
        using var leader = fixture.CreateLeaderClient();
        foreach (var suffix in new[] { "history", "replacement-candidates" })
        {
            using var denied = await volunteer.GetAsync($"{LeaderPath}/assignments/{assignment.Id}/{suffix}");
            using var hidden = await leader.GetAsync($"{LeaderPath}/assignments/{foreign.Id}/{suffix}");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }
        using var deniedWrite = await volunteer.DeleteAsync($"{LeaderPath}/assignments/{assignment.Id}?expectedVersion=1");
        using var hiddenWrite = await leader.DeleteAsync($"{LeaderPath}/assignments/{foreign.Id}?expectedVersion=1");
        using var deniedConflicts = await volunteer.GetAsync($"{LeaderPath}/conflicts");
        Assert.Equal(HttpStatusCode.Forbidden, deniedWrite.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deniedConflicts.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, hiddenWrite.StatusCode);
        var conflicts = await JsonAsync(await leader.GetAsync($"{LeaderPath}/conflicts"));
        Assert.DoesNotContain(conflicts.EnumerateArray(), x => x.GetProperty("id").GetGuid() == foreign.Id);
    }

    [Fact]
    public async Task Started_events_cannot_be_reassigned_or_cancelled()
    {
        var assignment = await fixture.CreateAssignmentAsync(started: true);
        using var leader = fixture.CreateLeaderClient();
        var path = $"{LeaderPath}/assignments/{assignment.Id}";
        using var update = await leader.PutAsJsonAsync(path, new { teamMemberId = assignment.TeamMemberId, roleName = "Setup", expectedVersion = 1 });
        using var cancel = await leader.DeleteAsync($"{path}?expectedVersion=1");
        using var preview = await leader.PostAsJsonAsync($"{path}/preview", new { teamMemberId = assignment.TeamMemberId, roleName = "Setup", expectedVersion = 1 });
        Assert.All(new[] { update, cancel, preview }, x => Assert.Equal(HttpStatusCode.Conflict, x.StatusCode));
    }
}

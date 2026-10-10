using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ManyRooms.Application;
using ManyRooms.Domain;
using ManyRooms.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ManyRooms.Api.Tests;

public sealed class LeaderSchedulingApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static string Path => $"/api/organizations/{DemoData.OrganizationId}";
    private static string LeaderEvent(Guid eventId) => $"{Path}/leader/events/{eventId}";

    [Fact]
    public async Task Demo_personas_select_fixed_database_members_and_do_not_accept_arbitrary_identity()
    {
        using var anonymous = fixture.CreateClient(demo: false);
        var volunteer = await anonymous.GetFromJsonAsync<DemoContextDto>("/api/demo/context");
        var leader = await anonymous.GetFromJsonAsync<DemoContextDto>("/api/demo/context?persona=leader");
        Assert.Equal(DemoData.UserId, volunteer!.UserId);
        Assert.Equal("Volunteer", volunteer.Role);
        Assert.Equal(DemoData.LeaderUserId, leader!.UserId);
        Assert.Equal("Leader", leader.Role);
        using var invalidPersona = await anonymous.GetAsync("/api/demo/context?persona=administrator");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPersona.StatusCode);
        anonymous.DefaultRequestHeaders.Add("X-ManyRooms-Demo", DemoData.LeaderUserId.ToString());
        using var arbitraryIdentity = await anonymous.GetAsync($"{Path}/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, arbitraryIdentity.StatusCode);
        using var leaderClient = fixture.CreateLeaderClient();
        var dashboard = await leaderClient.GetFromJsonAsync<DashboardDto>($"{Path}/dashboard");
        Assert.Equal(DemoData.LeaderUserId, dashboard!.CurrentUser.Id);
    }

    [Fact]
    public async Task Volunteers_cannot_access_leader_reads_previews_or_writes()
    {
        using var client = fixture.CreateClient();
        var request = new { teamMemberId = Guid.NewGuid(), roleName = "Welcome host" };
        using var details = await client.GetAsync(LeaderEvent(DemoData.FirstEventId));
        using var members = await client.GetAsync($"{Path}/leader/team-members");
        using var preview = await client.PostAsJsonAsync($"{LeaderEvent(DemoData.FirstEventId)}/assignments/preview", request);
        using var create = await client.PostAsJsonAsync($"{LeaderEvent(DemoData.FirstEventId)}/assignments", request);
        Assert.All(new[] { details, members, preview, create }, response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
    }

    [Fact]
    public async Task Leader_can_preview_and_create_advisory_conflicted_assignment_without_duplicate_writes()
    {
        using var client = fixture.CreateLeaderClient();
        var members = await client.GetFromJsonAsync<JsonElement>($"{Path}/leader/team-members");
        var member = members.EnumerateArray().First(x => x.GetProperty("volunteerId").GetGuid() == DemoData.UserId);
        var teamMemberId = member.GetProperty("id").GetGuid();
        var request = new { teamMemberId, roleName = "  Additional welcome support  " };
        await using var db = fixture.CreateDbContext();
        var countBefore = await db.Assignments.CountAsync();
        using var preview = await client.PostAsJsonAsync($"{LeaderEvent(DemoData.FirstEventId)}/assignments/preview", request);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewBody = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(previewBody.GetProperty("conflicts").EnumerateArray(), conflict =>
            conflict.GetProperty("type").GetString() == "OverlappingAssignment");
        Assert.Equal(countBefore, await db.Assignments.CountAsync());

        using var create = await client.PostAsJsonAsync($"{LeaderEvent(DemoData.FirstEventId)}/assignments", request);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var result = await create.Content.ReadFromJsonAsync<AssignmentDto>();
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
        Assert.Equal("Additional welcome support", result.RoleName);
        Assert.Equal(1, result.Version);
        Assert.Contains(result.Conflicts, conflict => conflict.Type == "OverlappingAssignment");
        var stored = await db.Assignments.SingleAsync(x => x.Id == result.Id);
        Assert.Equal(teamMemberId, stored.TeamMemberId);
        Assert.Equal(DemoData.OrganizationId, stored.OrganizationId);
        Assert.Equal(countBefore + 1, await db.Assignments.CountAsync());
        using var duplicate = await client.PostAsJsonAsync($"{LeaderEvent(DemoData.FirstEventId)}/assignments", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var refreshed = await client.GetFromJsonAsync<EventDetailsDto>(LeaderEvent(DemoData.FirstEventId));
        Assert.Contains(refreshed!.Assignments, x => x.Id == result.Id && x.Conflicts.Count > 0);
    }

    [Fact]
    public async Task Leader_role_is_read_from_membership_instead_of_trusting_demo_header()
    {
        await using var db = fixture.CreateDbContext();
        var membership = await db.Memberships.SingleAsync(x => x.OrganizationId == DemoData.OrganizationId && x.UserId == DemoData.LeaderUserId);
        membership.Role = MembershipRole.Volunteer;
        await db.SaveChangesAsync();
        try
        {
            using var client = fixture.CreateLeaderClient();
            using var response = await client.GetAsync(LeaderEvent(DemoData.FirstEventId));
            using var write = await client.PostAsJsonAsync($"{LeaderEvent(DemoData.FirstEventId)}/assignments",
                new { teamMemberId = Guid.NewGuid(), roleName = "Unauthorized" });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        }
        finally
        {
            membership.Role = MembershipRole.Leader;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Other_volunteers_conflicts_and_private_notes_are_hidden_but_leaders_can_see_the_conflict()
    {
        await using var db = fixture.CreateDbContext();
        var member = await db.TeamMembers.Include(x => x.Membership).FirstAsync(x =>
            x.OrganizationId == DemoData.OrganizationId && x.Membership.UserId == DemoData.LeaderUserId);
        var item = new Event
        {
            OrganizationId = DemoData.OrganizationId,
            Title = "Private availability check",
            Description = "Fictional event",
            Location = "Test hall",
            StartsAt = DateTimeOffset.UtcNow.AddDays(50),
            EndsAt = DateTimeOffset.UtcNow.AddDays(50).AddHours(2)
        };
        var assignment = new VolunteerAssignment
        {
            OrganizationId = item.OrganizationId,
            EventId = item.Id,
            Event = item,
            TeamMemberId = member.Id,
            RoleName = "Host"
        };
        const string privateNote = "Private appointment: do not disclose this note";
        item.StartsAt = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(50).AddHours(15), TimeSpan.Zero);
        item.EndsAt = item.StartsAt.AddHours(2);
        db.Assignments.Add(assignment);
        db.Unavailability.Add(VolunteerUnavailability.Create(item.OrganizationId, member.MembershipId,
            item.StartsAt.AddMinutes(30), item.EndsAt.AddHours(1), privateNote, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        using var volunteer = fixture.CreateClient();
        var volunteerDetails = await volunteer.GetFromJsonAsync<EventDetailsDto>($"{Path}/events/{item.Id}");
        Assert.Empty(Assert.Single(volunteerDetails!.Assignments).Conflicts);
        using var leader = fixture.CreateLeaderClient();
        using var leaderResponse = await leader.GetAsync(LeaderEvent(item.Id));
        Assert.Equal(HttpStatusCode.OK, leaderResponse.StatusCode);
        Assert.DoesNotContain(privateNote, await leaderResponse.Content.ReadAsStringAsync());
        var leaderDetails = await leaderResponse.Content.ReadFromJsonAsync<EventDetailsDto>();
        var conflict = Assert.Single(Assert.Single(leaderDetails!.Assignments).Conflicts);
        Assert.Equal("Unavailable", conflict.Type);
        Assert.Equal(item.StartsAt.AddMinutes(30), conflict.StartsAt);
        Assert.Equal(item.EndsAt, conflict.EndsAt);
    }

    [Fact]
    public async Task Assignment_response_returns_fresh_conflicts_and_declining_clears_them()
    {
        var assignment = await fixture.CreateAssignmentAsync();
        await using var db = fixture.CreateDbContext();
        var member = await db.TeamMembers.SingleAsync(x => x.Id == assignment.TeamMemberId);
        db.Unavailability.Add(VolunteerUnavailability.Create(assignment.OrganizationId, member.MembershipId,
            assignment.Event.StartsAt.AddMinutes(30), assignment.Event.EndsAt, "Private time", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        using var client = fixture.CreateClient();
        using var accept = await client.PutAsJsonAsync($"{Path}/assignments/{assignment.Id}/response", new RespondToAssignmentRequest("Accepted", 1));
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var accepted = await accept.Content.ReadFromJsonAsync<AssignmentDto>();
        Assert.Contains(accepted!.Conflicts, x => x.Type == "Unavailable");
        using var decline = await client.PutAsJsonAsync($"{Path}/assignments/{assignment.Id}/response", new RespondToAssignmentRequest("Declined", 2));
        Assert.Equal(HttpStatusCode.OK, decline.StatusCode);
        var declined = await decline.Content.ReadFromJsonAsync<AssignmentDto>();
        Assert.Empty(declined!.Conflicts);
        var refreshed = await client.GetFromJsonAsync<EventDetailsDto>($"{Path}/events/{assignment.EventId}");
        Assert.Empty(Assert.Single(refreshed!.Assignments).Conflicts);
    }

    [Fact]
    public async Task Leader_cannot_read_or_assign_across_organizations()
    {
        await using var db = fixture.CreateDbContext();
        var foreignEvent = await db.Events.SingleAsync(x => x.OrganizationId == DemoData.OtherOrganizationId);
        var foreignMember = await db.TeamMembers.SingleAsync(x => x.OrganizationId == DemoData.OtherOrganizationId);
        var ownMember = await db.TeamMembers.FirstAsync(x => x.OrganizationId == DemoData.OrganizationId);
        using var client = fixture.CreateLeaderClient();
        using var otherOrganization = await client.GetAsync($"/api/organizations/{DemoData.OtherOrganizationId}/leader/events/{foreignEvent.Id}");
        using var foreignRead = await client.GetAsync(LeaderEvent(foreignEvent.Id));
        using var foreignEventWrite = await client.PostAsJsonAsync($"{LeaderEvent(foreignEvent.Id)}/assignments", new { teamMemberId = ownMember.Id, roleName = "Host" });
        using var foreignMemberWrite = await client.PostAsJsonAsync($"{LeaderEvent(DemoData.FirstEventId)}/assignments", new { teamMemberId = foreignMember.Id, roleName = "Host" });
        using var foreignMemberPreview = await client.PostAsJsonAsync($"{LeaderEvent(DemoData.FirstEventId)}/assignments/preview", new { teamMemberId = foreignMember.Id, roleName = "Host" });
        Assert.Equal(HttpStatusCode.Forbidden, otherOrganization.StatusCode);
        Assert.All(new[] { foreignRead, foreignEventWrite, foreignMemberWrite, foreignMemberPreview }, response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
    }

    public static TheoryData<string?> InvalidRoles => new() { null, "", "   ", new string('x', 121) };

    [Theory]
    [MemberData(nameof(InvalidRoles))]
    public async Task Assignment_role_must_be_present_and_within_length_limit_on_preview_and_create(string? roleName)
    {
        await using var db = fixture.CreateDbContext();
        var member = await db.TeamMembers.FirstAsync(x => x.OrganizationId == DemoData.OrganizationId);
        using var client = fixture.CreateLeaderClient();
        using var preview = await client.PostAsJsonAsync($"{LeaderEvent(DemoData.FirstEventId)}/assignments/preview", new { teamMemberId = member.Id, roleName });
        using var create = await client.PostAsJsonAsync($"{LeaderEvent(DemoData.FirstEventId)}/assignments", new { teamMemberId = member.Id, roleName });
        Assert.Equal(HttpStatusCode.BadRequest, preview.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task Leader_cannot_create_assignment_after_event_start()
    {
        var assignment = await fixture.CreateAssignmentAsync(started: true);
        using var client = fixture.CreateLeaderClient();
        var request = new { teamMemberId = assignment.TeamMemberId, roleName = "New role" };
        using var preview = await client.PostAsJsonAsync($"{LeaderEvent(assignment.EventId)}/assignments/preview", request);
        using var create = await client.PostAsJsonAsync($"{LeaderEvent(assignment.EventId)}/assignments", request);
        Assert.Equal(HttpStatusCode.Conflict, preview.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, create.StatusCode);
    }
}

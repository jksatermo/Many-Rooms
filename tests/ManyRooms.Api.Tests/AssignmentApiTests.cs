using System.Net;
using System.Net.Http.Json;
using ManyRooms.Application;
using ManyRooms.Domain;
using ManyRooms.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ManyRooms.Api.Tests;

public sealed class AssignmentApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static string OrganizationPath => $"/api/organizations/{DemoData.OrganizationId}";
    private static string ResponsePath(Guid id) => $"{OrganizationPath}/assignments/{id}/response";

    [Fact]
    public async Task Demo_context_dashboard_and_events_expose_real_seeded_records()
    {
        using var client = fixture.CreateClient();
        var context = await client.GetFromJsonAsync<DemoContextDto>("/api/demo/context");
        var dashboard = await client.GetFromJsonAsync<DashboardDto>($"{OrganizationPath}/dashboard");
        var events = await client.GetFromJsonAsync<EventSummaryDto[]>($"{OrganizationPath}/events");

        Assert.NotNull(context);
        Assert.Equal(DemoData.OrganizationId, context.OrganizationId);
        Assert.Equal(DemoData.UserId, context.UserId);
        Assert.NotNull(dashboard);
        Assert.Equal(context.OrganizationName, dashboard.Organization.Name);
        Assert.Equal(context.DisplayName, dashboard.CurrentUser.DisplayName);
        Assert.NotNull(events);
        Assert.Contains(events, item => item.Id == DemoData.FirstEventId);
        Assert.All(dashboard.Assignments, item => Assert.Equal(DemoData.UserId, item.VolunteerId));
        Assert.Equal(events.Length, dashboard.Summary.UpcomingEvents);
    }

    [Fact]
    public async Task Protected_routes_require_explicit_demo_header()
    {
        using var client = fixture.CreateClient(demo: false);
        using var dashboard = await client.GetAsync($"{OrganizationPath}/dashboard");
        using var response = await client.PutAsJsonAsync(ResponsePath(DemoData.FirstAssignmentId), new RespondToAssignmentRequest("Accepted", 1));
        Assert.Equal(HttpStatusCode.Unauthorized, dashboard.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        client.DefaultRequestHeaders.Add("X-ManyRooms-Demo", "false");
        using var invalidHeader = await client.GetAsync($"{OrganizationPath}/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, invalidHeader.StatusCode);
    }

    [Fact]
    public async Task Demo_header_cannot_grant_membership_in_another_organization()
    {
        using var client = fixture.CreateClient();
        using var dashboard = await client.GetAsync($"/api/organizations/{DemoData.OtherOrganizationId}/dashboard");
        using var response = await client.PutAsJsonAsync(
            $"/api/organizations/{DemoData.OtherOrganizationId}/assignments/{DemoData.FirstAssignmentId}/response",
            new RespondToAssignmentRequest("Accepted", 1));

        Assert.Equal(HttpStatusCode.Forbidden, dashboard.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Other_organization_ids_cannot_be_read_or_updated_through_own_organization_route()
    {
        await using var db = fixture.CreateDbContext();
        var foreignAssignment = await db.Assignments.SingleAsync(x => x.OrganizationId == DemoData.OtherOrganizationId);
        using var client = fixture.CreateClient();
        using var eventResponse = await client.GetAsync($"{OrganizationPath}/events/{foreignAssignment.EventId}");
        using var assignmentResponse = await client.PutAsJsonAsync(ResponsePath(foreignAssignment.Id), new RespondToAssignmentRequest("Accepted", 1));

        Assert.Equal(HttpStatusCode.NotFound, eventResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, assignmentResponse.StatusCode);
    }

    [Fact]
    public async Task Volunteer_cannot_respond_for_another_volunteer()
    {
        var assignment = await fixture.CreateAssignmentAsync(own: false);
        using var client = fixture.CreateClient();
        using var response = await client.PutAsJsonAsync(ResponsePath(assignment.Id), new RespondToAssignmentRequest("Accepted", 1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertPendingAsync(assignment.Id);
    }

    [Fact]
    public async Task Accepted_and_declined_responses_survive_new_requests_and_database_contexts()
    {
        var assignment = await fixture.CreateAssignmentAsync();
        using var client = fixture.CreateClient();
        var before = await client.GetFromJsonAsync<DashboardDto>($"{OrganizationPath}/dashboard");

        using var acceptedResponse = await client.PutAsJsonAsync(ResponsePath(assignment.Id), new RespondToAssignmentRequest("Accepted", 1));
        Assert.Equal(HttpStatusCode.OK, acceptedResponse.StatusCode);
        var accepted = await acceptedResponse.Content.ReadFromJsonAsync<AssignmentDto>();
        Assert.NotNull(accepted);
        Assert.Equal("Accepted", accepted.Status);
        Assert.Equal(2, accepted.Version);
        Assert.NotNull(accepted.RespondedAt);

        using var refreshedClient = fixture.CreateClient();
        var refreshedEvent = await refreshedClient.GetFromJsonAsync<EventDetailsDto>($"{OrganizationPath}/events/{assignment.EventId}");
        Assert.NotNull(refreshedEvent);
        Assert.Equal("Accepted", Assert.Single(refreshedEvent.Assignments).Status);
        var after = await refreshedClient.GetFromJsonAsync<DashboardDto>($"{OrganizationPath}/dashboard");
        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.Equal(before.Summary.PendingResponses - 1, after.Summary.PendingResponses);
        Assert.Equal(before.Summary.AcceptedAssignments + 1, after.Summary.AcceptedAssignments);

        using var declinedResponse = await refreshedClient.PutAsJsonAsync(ResponsePath(assignment.Id), new RespondToAssignmentRequest("Declined", 2));
        Assert.Equal(HttpStatusCode.OK, declinedResponse.StatusCode);
        await using var db = fixture.CreateDbContext();
        var persisted = await db.Assignments.SingleAsync(x => x.Id == assignment.Id);
        var history = await db.Responses.Where(x => x.AssignmentId == assignment.Id).OrderBy(x => x.AssignmentVersion).ToListAsync();
        Assert.Equal(AssignmentStatus.Declined, persisted.Status);
        Assert.Equal(3, persisted.Version);
        Assert.Collection(history,
            first => { Assert.Equal(AssignmentStatus.Accepted, first.Decision); Assert.Equal(2, first.AssignmentVersion); },
            second => { Assert.Equal(AssignmentStatus.Declined, second.Decision); Assert.Equal(3, second.AssignmentVersion); });
    }

    [Theory]
    [InlineData("Pending", 1)]
    [InlineData("ACCEPT", 1)]
    [InlineData(null, 1)]
    [InlineData("Accepted", 0)]
    public async Task Invalid_response_returns_bad_request_without_writing_data(string? decision, int version)
    {
        var assignment = await fixture.CreateAssignmentAsync();
        using var client = fixture.CreateClient();
        using var response = await client.PutAsJsonAsync(ResponsePath(assignment.Id), new RespondToAssignmentRequest(decision, version));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertPendingAsync(assignment.Id);
    }

    [Fact]
    public async Task Missing_event_and_assignment_return_not_found()
    {
        using var client = fixture.CreateClient();
        using var eventResponse = await client.GetAsync($"{OrganizationPath}/events/{Guid.NewGuid()}");
        using var response = await client.PutAsJsonAsync(ResponsePath(Guid.NewGuid()), new RespondToAssignmentRequest("Accepted", 1));
        Assert.Equal(HttpStatusCode.NotFound, eventResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("{", "application/json", HttpStatusCode.BadRequest)]
    [InlineData("{\"decision\":\"Accepted\",\"expectedVersion\":\"wrong\"}", "application/json", HttpStatusCode.BadRequest)]
    [InlineData("Accepted", "text/plain", HttpStatusCode.UnsupportedMediaType)]
    public async Task Malformed_request_returns_client_error_without_writing_data(string body, string contentType, HttpStatusCode expected)
    {
        var assignment = await fixture.CreateAssignmentAsync();
        using var client = fixture.CreateClient();
        using var content = new StringContent(body, System.Text.Encoding.UTF8, contentType);
        using var response = await client.PutAsync(ResponsePath(assignment.Id), content);
        Assert.Equal(expected, response.StatusCode);
        await AssertPendingAsync(assignment.Id);
    }

    [Fact]
    public async Task Response_after_event_start_returns_conflict()
    {
        var assignment = await fixture.CreateAssignmentAsync(started: true);
        using var client = fixture.CreateClient();
        using var response = await client.PutAsJsonAsync(ResponsePath(assignment.Id), new RespondToAssignmentRequest("Accepted", 1));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertPendingAsync(assignment.Id);
    }

    [Fact]
    public async Task Stale_version_cannot_overwrite_a_persisted_response()
    {
        var assignment = await fixture.CreateAssignmentAsync();
        using var client = fixture.CreateClient();
        using var first = await client.PutAsJsonAsync(ResponsePath(assignment.Id), new RespondToAssignmentRequest("Accepted", 1));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var stale = await client.PutAsJsonAsync(ResponsePath(assignment.Id), new RespondToAssignmentRequest("Declined", 1));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.Equal(AssignmentStatus.Accepted, (await db.Assignments.SingleAsync(x => x.Id == assignment.Id)).Status);
        Assert.Single(await db.Responses.Where(x => x.AssignmentId == assignment.Id).ToListAsync());
    }

    [Fact]
    public async Task Concurrent_http_responses_allow_exactly_one_commit_and_one_conflict()
    {
        var assignment = await fixture.CreateAssignmentAsync();
        fixture.ConcurrentReads.Arm(assignment.Id);
        using var firstClient = fixture.CreateClient();
        using var secondClient = fixture.CreateClient();

        var responses = await Task.WhenAll(
            firstClient.PutAsJsonAsync(ResponsePath(assignment.Id), new RespondToAssignmentRequest("Accepted", 1)),
            secondClient.PutAsJsonAsync(ResponsePath(assignment.Id), new RespondToAssignmentRequest("Declined", 1)));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            var winner = await responses.Single(x => x.IsSuccessStatusCode).Content.ReadFromJsonAsync<AssignmentDto>();
            Assert.NotNull(winner);
            await using var db = fixture.CreateDbContext();
            var stored = await db.Assignments.SingleAsync(x => x.Id == assignment.Id);
            var history = Assert.Single(await db.Responses.Where(x => x.AssignmentId == assignment.Id).ToListAsync());
            Assert.Equal(winner.Status, stored.Status.ToString());
            Assert.Equal(stored.Status, history.Decision);
            Assert.Equal(2, stored.Version);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [Theory]
    [InlineData("team-member")]
    [InlineData("assignment-event")]
    [InlineData("assignment-volunteer")]
    [InlineData("response-membership")]
    public async Task Database_foreign_keys_reject_cross_organization_relationships(string relationship)
    {
        await using var db = fixture.CreateDbContext();
        var ownMembership = await db.Memberships.FirstAsync(x => x.OrganizationId == DemoData.OrganizationId);
        var foreignMembership = await db.Memberships.SingleAsync(x => x.OrganizationId == DemoData.OtherOrganizationId);
        var ownMember = await db.TeamMembers.FirstAsync(x => x.OrganizationId == DemoData.OrganizationId);
        var foreignMember = await db.TeamMembers.SingleAsync(x => x.OrganizationId == DemoData.OtherOrganizationId);
        if (relationship == "team-member")
        {
            db.TeamMembers.Add(new TeamMember
            {
                OrganizationId = DemoData.OrganizationId,
                MembershipId = ownMembership.Id,
                TeamId = foreignMember.TeamId
            });
        }
        else if (relationship == "response-membership")
        {
            db.Responses.Add(new AssignmentResponse
            {
                OrganizationId = DemoData.OrganizationId,
                AssignmentId = DemoData.FirstAssignmentId,
                MembershipId = foreignMembership.Id,
                Decision = AssignmentStatus.Accepted,
                AssignmentVersion = 999,
                RespondedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            var foreignEvent = await db.Events.SingleAsync(x => x.OrganizationId == DemoData.OtherOrganizationId);
            db.Assignments.Add(new VolunteerAssignment
            {
                OrganizationId = DemoData.OrganizationId,
                EventId = relationship == "assignment-event" ? foreignEvent.Id : DemoData.FirstEventId,
                TeamMemberId = relationship == "assignment-volunteer" ? foreignMember.Id : ownMember.Id,
                RoleName = "Invalid cross-organization assignment"
            });
        }

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [Fact]
    public async Task Repeating_seed_preserves_existing_responses_without_duplicate_records()
    {
        using var client = fixture.CreateClient();
        using var response = await client.PutAsJsonAsync(ResponsePath(DemoData.FirstAssignmentId), new RespondToAssignmentRequest("Accepted", 1));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateDbContext();
        var assignmentsBefore = await db.Assignments.CountAsync();
        var responsesBefore = await db.Responses.CountAsync();

        await DemoData.SeedAsync(db, TimeProvider.System);

        Assert.Equal(assignmentsBefore, await db.Assignments.CountAsync());
        Assert.Equal(responsesBefore, await db.Responses.CountAsync());
        Assert.Equal(AssignmentStatus.Accepted, (await db.Assignments.SingleAsync(x => x.Id == DemoData.FirstAssignmentId)).Status);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Production_disables_demo_context_and_rejects_demo_identity()
    {
        await using var factory = new DemoApiFactory(fixture.ConnectionString, environment: "Production", demoEnabled: false);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-ManyRooms-Demo", "true");
        using var context = await client.GetAsync("/api/demo/context");
        using var dashboard = await client.GetAsync($"{OrganizationPath}/dashboard");

        Assert.Equal(HttpStatusCode.NotFound, context.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, dashboard.StatusCode);
    }

    [Fact]
    public async Task Production_refuses_to_start_when_demo_is_accidentally_enabled()
    {
        await using var factory = new DemoApiFactory(fixture.ConnectionString, environment: "Production", demoEnabled: true);
        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("demo identity may only be enabled in Development", exception.Message);
    }

    private async Task AssertPendingAsync(Guid assignmentId)
    {
        await using var db = fixture.CreateDbContext();
        var assignment = await db.Assignments.SingleAsync(x => x.Id == assignmentId);
        Assert.Equal(AssignmentStatus.Pending, assignment.Status);
        Assert.Equal(1, assignment.Version);
        Assert.Null(assignment.RespondedAt);
        Assert.Empty(await db.Responses.Where(x => x.AssignmentId == assignmentId).ToListAsync());
    }
}

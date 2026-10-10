using ManyRooms.Application;
using ManyRooms.Domain;

namespace ManyRooms.Application.Tests;

public sealed class AssignmentResponseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(AssignmentStatus.Pending, "Accepted", AssignmentStatus.Accepted)]
    [InlineData(AssignmentStatus.Pending, "Declined", AssignmentStatus.Declined)]
    [InlineData(AssignmentStatus.Accepted, "Declined", AssignmentStatus.Declined)]
    [InlineData(AssignmentStatus.Declined, "Accepted", AssignmentStatus.Accepted)]
    public async Task Volunteer_can_respond_and_change_response_before_event_starts(
        AssignmentStatus original, string decision, AssignmentStatus expected)
    {
        var assignment = CreateAssignment();
        assignment.Status = original;
        assignment.RespondedAt = original == AssignmentStatus.Pending ? null : Now.AddHours(-1);
        var store = new FakeAssignmentStore(assignment);

        var result = await Service(store).RespondAsync(assignment.OrganizationId, assignment.Id,
            assignment.TeamMember.Membership.UserId, new(decision, 1), CancellationToken.None);

        Assert.Equal(expected, assignment.Status);
        Assert.Equal(expected.ToString(), result.Status);
        Assert.Equal(2, result.Version);
        Assert.Equal(Now, result.RespondedAt);
        Assert.Equal(Now, assignment.UpdatedAt);
        Assert.True(result.IsCurrentUser);
        var history = Assert.Single(store.Responses);
        Assert.Equal(assignment.Id, history.AssignmentId);
        Assert.Equal(assignment.OrganizationId, history.OrganizationId);
        Assert.Equal(assignment.TeamMember.MembershipId, history.MembershipId);
        Assert.Equal(expected, history.Decision);
        Assert.Equal(2, history.AssignmentVersion);
        Assert.Equal(Now, history.RespondedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Pending")]
    [InlineData("accepted")]
    [InlineData("ACCEPT")]
    public async Task Invalid_decision_cannot_change_assignment_or_create_history(string? decision)
    {
        var assignment = CreateAssignment();
        var store = new FakeAssignmentStore(assignment);

        await Assert.ThrowsAsync<ArgumentException>(() => Service(store).RespondAsync(
            assignment.OrganizationId, assignment.Id, assignment.TeamMember.Membership.UserId,
            new(decision, 1), CancellationToken.None));

        AssertUnchanged(assignment, store);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Expected_version_must_be_positive(int version)
    {
        var assignment = CreateAssignment();
        var store = new FakeAssignmentStore(assignment);

        await Assert.ThrowsAsync<ArgumentException>(() => Service(store).RespondAsync(
            assignment.OrganizationId, assignment.Id, assignment.TeamMember.Membership.UserId,
            new("Accepted", version), CancellationToken.None));

        AssertUnchanged(assignment, store);
    }

    [Fact]
    public async Task Stale_response_requires_refresh_instead_of_overwriting_newer_response()
    {
        var assignment = CreateAssignment();
        assignment.Version = 2;
        assignment.Status = AssignmentStatus.Declined;
        var store = new FakeAssignmentStore(assignment);

        await Assert.ThrowsAsync<AssignmentConflictException>(() => Service(store).RespondAsync(
            assignment.OrganizationId, assignment.Id, assignment.TeamMember.Membership.UserId,
            new("Accepted", 1), CancellationToken.None));

        Assert.Equal(AssignmentStatus.Declined, assignment.Status);
        Assert.Equal(2, assignment.Version);
        Assert.Empty(store.Responses);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Responses_close_at_event_start(int minutesFromNow)
    {
        var assignment = CreateAssignment();
        assignment.Event.StartsAt = Now.AddMinutes(minutesFromNow);
        var store = new FakeAssignmentStore(assignment);

        await Assert.ThrowsAsync<AssignmentConflictException>(() => Service(store).RespondAsync(
            assignment.OrganizationId, assignment.Id, assignment.TeamMember.Membership.UserId,
            new("Accepted", 1), CancellationToken.None));

        AssertUnchanged(assignment, store);
    }

    [Fact]
    public async Task Organization_membership_is_required_before_reading_assignment()
    {
        var assignment = CreateAssignment();
        var store = new FakeAssignmentStore(assignment) { IsMember = false };

        await Assert.ThrowsAsync<AssignmentAccessException>(() => Service(store).RespondAsync(
            assignment.OrganizationId, assignment.Id, assignment.TeamMember.Membership.UserId,
            new("Accepted", 1), CancellationToken.None));

        Assert.Equal(0, store.FindCalls);
        AssertUnchanged(assignment, store);
    }

    [Fact]
    public async Task Another_organization_member_cannot_respond_for_assigned_volunteer()
    {
        var assignment = CreateAssignment();
        var store = new FakeAssignmentStore(assignment);

        await Assert.ThrowsAsync<AssignmentAccessException>(() => Service(store).RespondAsync(
            assignment.OrganizationId, assignment.Id, Guid.NewGuid(), new("Accepted", 1), CancellationToken.None));

        AssertUnchanged(assignment, store);
    }

    [Fact]
    public async Task Assignment_lookup_is_scoped_to_requested_organization()
    {
        var assignment = CreateAssignment();
        var store = new FakeAssignmentStore(assignment);

        await Assert.ThrowsAsync<AssignmentNotFoundException>(() => Service(store).RespondAsync(
            Guid.NewGuid(), assignment.Id, assignment.TeamMember.Membership.UserId,
            new("Accepted", 1), CancellationToken.None));

        AssertUnchanged(assignment, store);
    }

    [Fact]
    public async Task Missing_assignment_returns_not_found_without_creating_response()
    {
        var store = new FakeAssignmentStore(null);

        await Assert.ThrowsAsync<AssignmentNotFoundException>(() => Service(store).RespondAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new("Accepted", 1), CancellationToken.None));

        Assert.Empty(store.Responses);
    }

    private static AssignmentResponseService Service(FakeAssignmentStore store) => new(store, new FixedClock());

    private static void AssertUnchanged(VolunteerAssignment assignment, FakeAssignmentStore store)
    {
        Assert.Equal(AssignmentStatus.Pending, assignment.Status);
        Assert.Null(assignment.RespondedAt);
        Assert.Equal(1, assignment.Version);
        Assert.Empty(store.Responses);
    }

    private static VolunteerAssignment CreateAssignment()
    {
        var organizationId = Guid.NewGuid();
        var user = new User { DisplayName = "Demo volunteer", Email = "volunteer@example.test" };
        var membership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            User = user
        };
        var team = new Team { OrganizationId = organizationId, Name = "Welcome team" };
        var teamMember = new TeamMember
        {
            OrganizationId = organizationId,
            TeamId = team.Id,
            Team = team,
            MembershipId = membership.Id,
            Membership = membership
        };
        var item = new Event
        {
            OrganizationId = organizationId,
            Title = "Community gathering",
            Description = "Everyone is welcome.",
            Location = "Community hall",
            StartsAt = Now.AddDays(1),
            EndsAt = Now.AddDays(1).AddHours(1)
        };
        return new VolunteerAssignment
        {
            OrganizationId = organizationId,
            EventId = item.Id,
            Event = item,
            TeamMemberId = teamMember.Id,
            TeamMember = teamMember,
            RoleName = "Greeter"
        };
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeAssignmentStore(VolunteerAssignment? assignment) : IAssignmentStore
    {
        public bool IsMember { get; init; } = true;
        public int FindCalls { get; private set; }
        public List<AssignmentResponse> Responses { get; } = [];

        public Task<bool> IsMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(IsMember);

        public Task<VolunteerAssignment?> FindAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken)
        {
            FindCalls++;
            return Task.FromResult(assignment?.OrganizationId == organizationId && assignment.Id == assignmentId
                ? assignment : null);
        }

        public Task SaveResponseAsync(VolunteerAssignment item, AssignmentResponse response, CancellationToken cancellationToken)
        {
            Responses.Add(response);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ScheduleConflictDto>> GetConflictsAsync(VolunteerAssignment item, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ScheduleConflictDto>>([]);
    }
}

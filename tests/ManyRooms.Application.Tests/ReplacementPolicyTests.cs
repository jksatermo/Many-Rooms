using ManyRooms.Domain;

namespace ManyRooms.Application.Tests;

public sealed class ReplacementPolicyTests
{
    private static readonly DateTimeOffset Start = new(2027, 1, 10, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Candidates_must_be_active_in_the_same_organization_team_and_a_different_membership()
    {
        var target = Assignment();
        var valid = Member(target, "Eligible");
        var inactive = Member(target, "Inactive");
        inactive.IsActive = false;
        var foreign = Member(target, "Foreign organization");
        foreign.OrganizationId = Guid.NewGuid();
        var otherTeam = Member(target, "Other team");
        otherTeam.TeamId = Guid.NewGuid();
        var duplicateMembership = Member(target, "Current volunteer");
        duplicateMembership.MembershipId = target.TeamMember.MembershipId;
        var candidates = ReplacementPolicy.Suggest(target, [target.TeamMember, valid, inactive, foreign, otherTeam, duplicateMembership], [], []);
        Assert.Equal(valid.Id, Assert.Single(candidates).TeamMemberId);
    }

    [Theory]
    [InlineData(-60, 60, false)]
    [InlineData(0, 120, false)]
    [InlineData(30, 90, false)]
    [InlineData(-30, 150, false)]
    [InlineData(-120, 0, true)]
    [InlineData(120, 240, true)]
    public void Candidates_are_filtered_by_actual_assignment_overlap_including_adjacent_boundaries(int start, int end, bool eligible)
    {
        var target = Assignment();
        var member = Member(target);
        var conflict = Assignment(target, member, Start.AddMinutes(start), Start.AddMinutes(end));
        Assert.Equal(eligible ? 1 : 0, ReplacementPolicy.Suggest(target, [member], [target, conflict], []).Count);
    }

    [Theory]
    [InlineData(-60, 60, false)]
    [InlineData(-30, 150, false)]
    [InlineData(-120, 0, true)]
    [InlineData(120, 240, true)]
    public void Candidates_are_filtered_by_unavailable_intersections_without_treating_adjacency_as_overlap(int start, int end, bool eligible)
    {
        var target = Assignment();
        var member = Member(target);
        var unavailable = VolunteerUnavailability.Create(target.OrganizationId, member.MembershipId,
            Start.AddMinutes(start), Start.AddMinutes(end), "Private", Start.AddDays(-1));
        Assert.Equal(eligible ? 1 : 0, ReplacementPolicy.Suggest(target, [member], [], [unavailable]).Count);
    }

    [Fact]
    public void Other_organizations_declined_cancelled_and_different_volunteers_do_not_exclude_a_candidate()
    {
        var target = Assignment();
        var member = Member(target);
        var foreign = Assignment(target, member);
        foreign.OrganizationId = Guid.NewGuid();
        var declined = Assignment(target, member);
        declined.Status = AssignmentStatus.Declined;
        var cancelled = Assignment(target, member);
        cancelled.IsCancelled = true;
        var differentVolunteer = Assignment(target, Member(target));
        var foreignUnavailable = VolunteerUnavailability.Create(Guid.NewGuid(), member.MembershipId, Start, Start.AddHours(2), null, Start);
        Assert.Equal(member.Id, Assert.Single(ReplacementPolicy.Suggest(target, [member], [foreign, declined, cancelled, differentVolunteer], [foreignUnavailable])).TeamMemberId);
    }

    [Theory]
    [InlineData(AssignmentStatus.Pending)]
    [InlineData(AssignmentStatus.Accepted)]
    [InlineData(AssignmentStatus.Declined)]
    public void An_existing_same_event_team_and_normalized_role_is_not_suggested_again(AssignmentStatus status)
    {
        var target = Assignment();
        var member = Member(target);
        var sameSlot = Assignment(target, member);
        sameSlot.EventId = target.EventId;
        sameSlot.Event = target.Event;
        sameSlot.RoleName = "  hOsT ";
        sameSlot.Status = status;
        Assert.Empty(ReplacementPolicy.Suggest(target, [member], [sameSlot], []));
        sameSlot.IsCancelled = true;
        Assert.Single(ReplacementPolicy.Suggest(target, [member], [sameSlot], []));
    }

    [Fact]
    public void Ranking_uses_role_preference_then_bounded_active_workload_then_name_then_membership_id()
    {
        var target = Assignment();
        var matchingBusy = Member(target, "Aaron", "  HOST ");
        var matchingFree = Member(target, "Zoe", "Host");
        var nameB = Member(target, "Blair");
        var nameA = Member(target, "avery");
        var nameATie = Member(target, "Avery");
        nameA.MembershipId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        nameATie.MembershipId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var counted = Assignment(target, matchingBusy, Start.AddDays(1));
        var before = Assignment(target, matchingFree, Start.AddDays(-1));
        var endBoundary = Assignment(target, matchingFree, Start.AddDays(28));
        var declined = Assignment(target, matchingFree, Start.AddDays(2));
        declined.Status = AssignmentStatus.Declined;
        var cancelled = Assignment(target, matchingFree, Start.AddDays(3));
        cancelled.IsCancelled = true;
        var foreign = Assignment(target, matchingFree, Start.AddDays(4));
        foreign.OrganizationId = Guid.NewGuid();
        var results = ReplacementPolicy.Suggest(target, [nameB, matchingBusy, nameATie, matchingFree, nameA],
            [target, counted, before, endBoundary, declined, cancelled, foreign], []);
        Assert.Equal(new[] { matchingFree.Id, matchingBusy.Id, nameA.Id, nameATie.Id, nameB.Id }, results.Select(x => x.TeamMemberId));
        Assert.Equal(new[] { 0, 1, 0, 0, 0 }, results.Select(x => x.UpcomingAssignmentCount));
        Assert.True(results[0].RoleMatch);
        Assert.False(results[2].RoleMatch);
    }

    private static TeamMember Member(VolunteerAssignment target, string name = "Volunteer", string? position = null)
    {
        var user = new User { DisplayName = name, Email = $"{Guid.NewGuid():N}@example.invalid" };
        var membership = new OrganizationMembership { OrganizationId = target.OrganizationId, UserId = user.Id, User = user };
        return new TeamMember
        {
            OrganizationId = target.OrganizationId, TeamId = target.TeamMember.TeamId, MembershipId = membership.Id,
            Membership = membership, PositionName = position
        };
    }

    private static VolunteerAssignment Assignment(VolunteerAssignment? target = null, TeamMember? member = null,
        DateTimeOffset? startsAt = null, DateTimeOffset? endsAt = null)
    {
        var organizationId = target?.OrganizationId ?? Guid.NewGuid();
        var user = new User { DisplayName = "Original volunteer", Email = "original@example.invalid" };
        var membership = new OrganizationMembership { OrganizationId = organizationId, UserId = user.Id, User = user };
        member ??= new TeamMember { OrganizationId = organizationId, TeamId = Guid.NewGuid(), MembershipId = membership.Id, Membership = membership };
        var item = new Event
        {
            OrganizationId = organizationId, Title = "Gathering", Description = "Fictional", Location = "Community room",
            StartsAt = startsAt ?? Start, EndsAt = endsAt ?? (startsAt ?? Start).AddHours(2)
        };
        return new VolunteerAssignment { OrganizationId = organizationId, TeamMember = member, TeamMemberId = member.Id, Event = item, EventId = item.Id, RoleName = "Host" };
    }
}

using ManyRooms.Domain;

namespace ManyRooms.Application.Tests;

public sealed class ScheduleConflictTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 15, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, 120, 0, 120)] // Exact overlap.
    [InlineData(-60, 60, 0, 60)] // Begins before the target.
    [InlineData(60, 180, 60, 120)] // Ends after the target.
    [InlineData(30, 90, 30, 90)] // Contained by the target.
    [InlineData(-30, 150, 0, 120)] // Contains the target.
    public void Assignment_conflict_returns_actual_intersection(int otherStart, int otherEnd, int intersectionStart, int intersectionEnd)
    {
        var target = Assignment();
        var other = Assignment(target, otherStart, otherEnd);

        var conflict = Assert.Single(ScheduleConflictDetector.Detect(target, [target, other], []));

        Assert.Equal("OverlappingAssignment", conflict.Type);
        Assert.Equal(Start.AddMinutes(intersectionStart), conflict.StartsAt);
        Assert.Equal(Start.AddMinutes(intersectionEnd), conflict.EndsAt);
        Assert.Equal(other.Id, conflict.RelatedAssignmentId);
        Assert.Equal(other.EventId, conflict.RelatedEventId);
        Assert.Equal(other.Event.Title, conflict.RelatedEventTitle);
        Assert.Null(conflict.AvailabilityId);
    }

    [Theory]
    [InlineData(-120, 0)]
    [InlineData(120, 240)]
    [InlineData(121, 240)]
    public void Adjacent_or_separated_events_do_not_conflict(int otherStart, int otherEnd)
    {
        var target = Assignment();
        Assert.Empty(ScheduleConflictDetector.Detect(target, [Assignment(target, otherStart, otherEnd)], []));
    }

    [Theory]
    [InlineData(-30, 30, 0, 30)]
    [InlineData(-30, 150, 0, 120)]
    [InlineData(30, 90, 30, 90)]
    public void Unavailability_reports_intersection_without_private_note(int unavailableStart, int unavailableEnd, int intersectionStart, int intersectionEnd)
    {
        var target = Assignment();
        var unavailable = Unavailable(target, unavailableStart, unavailableEnd);

        var conflict = Assert.Single(ScheduleConflictDetector.Detect(target, [], [unavailable]));

        Assert.Equal("Unavailable", conflict.Type);
        Assert.Equal(Start.AddMinutes(intersectionStart), conflict.StartsAt);
        Assert.Equal(Start.AddMinutes(intersectionEnd), conflict.EndsAt);
        Assert.Equal(unavailable.Id, conflict.AvailabilityId);
        Assert.Null(conflict.RelatedAssignmentId);
        Assert.Null(conflict.RelatedEventId);
        Assert.DoesNotContain(unavailable.Note!, conflict.Message);
    }

    [Theory]
    [InlineData(-120, 0)]
    [InlineData(120, 240)]
    public void Adjacent_unavailable_time_is_not_a_conflict(int unavailableStart, int unavailableEnd)
    {
        var target = Assignment();
        Assert.Empty(ScheduleConflictDetector.Detect(target, [], [Unavailable(target, unavailableStart, unavailableEnd)]));
    }

    [Fact]
    public void Same_person_on_different_teams_is_still_double_booked()
    {
        var target = Assignment();
        var other = Assignment(target);
        Assert.NotEqual(target.TeamMemberId, other.TeamMemberId);
        Assert.Single(ScheduleConflictDetector.Detect(target, [other], []));
    }

    [Fact]
    public void Another_volunteers_assignments_and_unavailability_do_not_conflict()
    {
        var target = Assignment();
        var other = Assignment();
        other.OrganizationId = target.OrganizationId;
        other.Event.OrganizationId = target.OrganizationId;
        other.TeamMember.OrganizationId = target.OrganizationId;
        other.TeamMember.Membership.OrganizationId = target.OrganizationId;
        var unavailable = Unavailable(other);
        Assert.Empty(ScheduleConflictDetector.Detect(target, [other], [unavailable]));
    }

    [Fact]
    public void Other_organization_records_are_ignored_even_with_matching_person_ids()
    {
        var target = Assignment();
        var other = Assignment(target);
        other.OrganizationId = Guid.NewGuid();
        var unavailable = Unavailable(target);
        unavailable.OrganizationId = other.OrganizationId;
        Assert.Empty(ScheduleConflictDetector.Detect(target, [other], [unavailable]));
    }

    [Fact]
    public void Multiple_roles_at_same_event_are_not_double_bookings()
    {
        var target = Assignment();
        var other = Assignment(target);
        other.EventId = target.EventId;
        other.Event = target.Event;
        Assert.Empty(ScheduleConflictDetector.Detect(target, [target, other], []));
    }

    [Fact]
    public void Declined_assignment_does_not_block_another_assignment()
    {
        var target = Assignment();
        var other = Assignment(target);
        other.Status = AssignmentStatus.Declined;
        Assert.Empty(ScheduleConflictDetector.Detect(target, [other], []));
    }

    [Fact]
    public void Declined_target_has_no_conflicts_even_when_otherwise_overlapping()
    {
        var target = Assignment();
        target.Status = AssignmentStatus.Declined;
        Assert.Empty(ScheduleConflictDetector.Detect(target, [Assignment(target)], [Unavailable(target)]));
    }

    [Fact]
    public void Comparison_uses_instants_across_different_offsets()
    {
        var target = Assignment();
        var other = Assignment(target);
        other.Event.StartsAt = Start.ToOffset(TimeSpan.FromHours(-6));
        other.Event.EndsAt = Start.AddHours(2).ToOffset(TimeSpan.FromHours(-6));
        var conflict = Assert.Single(ScheduleConflictDetector.Detect(target, [other], []));
        Assert.Equal(Start, conflict.StartsAt);
        Assert.Equal(Start.AddHours(2), conflict.EndsAt);
    }

    private static VolunteerUnavailability Unavailable(VolunteerAssignment target, int start = 0, int end = 120) =>
        VolunteerUnavailability.Create(target.OrganizationId, target.TeamMember.MembershipId,
            Start.AddMinutes(start), Start.AddMinutes(end), "Private family appointment", Start.AddDays(-1));

    private static VolunteerAssignment Assignment(VolunteerAssignment? sameVolunteer = null, int start = 0, int end = 120)
    {
        var organizationId = sameVolunteer?.OrganizationId ?? Guid.NewGuid();
        var membership = sameVolunteer?.TeamMember.Membership ?? new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = Guid.NewGuid()
        };
        var member = new TeamMember { OrganizationId = organizationId, MembershipId = membership.Id, Membership = membership };
        var item = new Event
        {
            OrganizationId = organizationId,
            Title = "Community meal",
            Description = "Fictional gathering",
            Location = "Community hall",
            StartsAt = Start.AddMinutes(start),
            EndsAt = Start.AddMinutes(end)
        };
        return new VolunteerAssignment
        {
            OrganizationId = organizationId,
            TeamMemberId = member.Id,
            TeamMember = member,
            EventId = item.Id,
            Event = item,
            RoleName = "Welcome host"
        };
    }
}

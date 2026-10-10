using ManyRooms.Domain;

namespace ManyRooms.Application.Tests;

public sealed class VolunteerUnavailabilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Creation_normalizes_offset_to_utc_and_trims_private_note()
    {
        var organizationId = Guid.NewGuid();
        var membershipId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.FromHours(-6));
        var item = VolunteerUnavailability.Create(organizationId, membershipId, start, start.AddHours(2), "  Family visit  ", Now);
        Assert.Equal(organizationId, item.OrganizationId);
        Assert.Equal(membershipId, item.MembershipId);
        Assert.Equal(start.ToUniversalTime(), item.StartsAt);
        Assert.Equal(TimeSpan.Zero, item.StartsAt.Offset);
        Assert.Equal(TimeSpan.Zero, item.EndsAt.Offset);
        Assert.Equal("Family visit", item.Note);
        Assert.Equal(Now, item.CreatedAt);
        Assert.Equal(Now, item.UpdatedAt);
        Assert.Equal(1, item.Version);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Empty_or_reversed_range_is_invalid(int endHours) =>
        Assert.Throws<ArgumentException>(() => VolunteerUnavailability.Create(Guid.NewGuid(), Guid.NewGuid(), Now, Now.AddHours(endHours), null, Now));

    [Fact]
    public void Excessively_long_note_is_rejected() =>
        Assert.Throws<ArgumentException>(() => VolunteerUnavailability.Create(Guid.NewGuid(), Guid.NewGuid(), Now, Now.AddHours(1), new string('x', 501), Now));

    [Fact]
    public void Replacing_range_advances_version_and_preserves_creation_time()
    {
        var item = Create();
        item.Replace(Now.AddDays(1), Now.AddDays(1).AddHours(2), "  ", 1, Now.AddHours(1));
        Assert.Equal(Now.AddDays(1), item.StartsAt);
        Assert.Equal(2, item.Version);
        Assert.Equal(Now, item.CreatedAt);
        Assert.Equal(Now.AddHours(1), item.UpdatedAt);
        Assert.Null(item.Note);
    }

    [Fact]
    public void Stale_edit_preserves_the_current_range()
    {
        var item = Create();
        item.Replace(Now.AddDays(1), Now.AddDays(1).AddHours(1), "Updated", 1, Now);
        Assert.Throws<AssignmentConflictException>(() => item.Replace(Now.AddDays(2), Now.AddDays(2).AddHours(1), "Stale", 1, Now));
        Assert.Equal(Now.AddDays(1), item.StartsAt);
        Assert.Equal("Updated", item.Note);
        Assert.Equal(2, item.Version);
    }

    [Fact]
    public void Invalid_edit_preserves_the_current_range()
    {
        var item = Create();
        Assert.Throws<ArgumentException>(() => item.Replace(Now.AddHours(3), Now, "Invalid", 1, Now));
        Assert.Equal(Now, item.StartsAt);
        Assert.Equal(Now.AddHours(1), item.EndsAt);
        Assert.Equal(1, item.Version);
    }

    private static VolunteerUnavailability Create() =>
        VolunteerUnavailability.Create(Guid.NewGuid(), Guid.NewGuid(), Now, Now.AddHours(1), null, Now);
}

using ManyRooms.Application;
using ManyRooms.Domain;

namespace ManyRooms.Application.Tests;

public sealed class RecurrenceTests
{
    private static readonly DateTimeOffset BeforeSpring = new(2027, 3, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Weekly_instances_keep_local_wall_time_across_spring_offset_change()
    {
        var template = Template(new(2027, 3, 7), new(10, 0));
        var result = RecurrencePlanner.Plan(template, new(2027, 3, 14), BeforeSpring, new HashSet<DateOnly>());
        Assert.Equal(2, result.Occurrences.Count);
        Assert.Equal(new DateTimeOffset(2027, 3, 7, 16, 0, 0, TimeSpan.Zero), result.Occurrences[0].StartsAt);
        Assert.Equal(new DateTimeOffset(2027, 3, 14, 15, 0, 0, TimeSpan.Zero), result.Occurrences[1].StartsAt);
        Assert.All(result.Occurrences, item => Assert.Equal(TimeSpan.FromMinutes(90), item.EndsAt - item.StartsAt));
    }

    [Fact]
    public void Nonexistent_spring_clock_time_is_skipped_and_reported()
    {
        var result = RecurrencePlanner.Plan(Template(new(2027, 3, 7), new(2, 30)), new(2027, 3, 21), BeforeSpring, new HashSet<DateOnly>());
        Assert.Equal(2, result.Occurrences.Count);
        Assert.Equal("2027-03-14", Assert.Single(result.SkippedDates));
        Assert.DoesNotContain(result.Occurrences, item => item.Date == new DateOnly(2027, 3, 14));
    }

    [Fact]
    public void Repeated_fall_clock_time_uses_earlier_instant_and_elapsed_duration()
    {
        var template = Template(new(2027, 11, 7), new(1, 30));
        var result = RecurrencePlanner.Plan(template, template.StartsOn, BeforeSpring, new HashSet<DateOnly>());
        var occurrence = Assert.Single(result.Occurrences);
        Assert.Equal(new DateTimeOffset(2027, 11, 7, 6, 30, 0, TimeSpan.Zero), occurrence.StartsAt);
        Assert.Equal(new DateTimeOffset(2027, 11, 7, 8, 0, 0, TimeSpan.Zero), occurrence.EndsAt);
    }

    [Fact]
    public void Bounded_range_is_inclusive_and_accepts_exactly_84_calendar_days()
    {
        var template = Template(new(2027, 3, 7), new(10, 0));
        var result = RecurrencePlanner.Plan(template, template.StartsOn.AddDays(83), BeforeSpring, new HashSet<DateOnly>());
        Assert.Equal(12, result.Occurrences.Count);
        Assert.Throws<ArgumentException>(() => RecurrencePlanner.Plan(template, template.StartsOn.AddDays(84), BeforeSpring, new HashSet<DateOnly>()));
        Assert.Throws<ArgumentException>(() => RecurrencePlanner.Plan(template, template.StartsOn.AddDays(-1), BeforeSpring, new HashSet<DateOnly>()));
    }

    [Fact]
    public void Generation_starts_at_organization_today_not_utc_today_or_historical_start()
    {
        var template = Template(new(2027, 2, 1), new(20, 0));
        // UTC Monday is still Sunday evening in Chicago.
        var now = new DateTimeOffset(2027, 3, 8, 0, 0, 0, TimeSpan.Zero);
        var result = RecurrencePlanner.Plan(template, new(2027, 3, 7), now, new HashSet<DateOnly>());
        Assert.Equal(new DateOnly(2027, 3, 7), Assert.Single(result.Occurrences).Date);
    }

    [Fact]
    public void Started_today_is_skipped_and_existing_today_counts_without_regeneration()
    {
        var template = Template(new(2027, 3, 7), new(10, 0));
        var now = new DateTimeOffset(2027, 3, 7, 16, 0, 0, TimeSpan.Zero);
        var first = RecurrencePlanner.Plan(template, template.StartsOn, now, new HashSet<DateOnly>());
        Assert.Empty(first.Occurrences);
        Assert.Equal("2027-03-07", Assert.Single(first.SkippedDates));
        var existing = RecurrencePlanner.Plan(template, template.StartsOn, now, new HashSet<DateOnly> { template.StartsOn });
        Assert.Empty(existing.Occurrences);
        Assert.Empty(existing.SkippedDates);
        Assert.Equal(1, existing.ExistingCount);
    }

    [Fact]
    public void Repeated_generation_counts_existing_dates_and_creates_only_missing_dates()
    {
        var template = Template(new(2027, 3, 7), new(10, 0));
        var result = RecurrencePlanner.Plan(template, new(2027, 3, 21), BeforeSpring,
            new HashSet<DateOnly> { new(2027, 3, 7), new(2027, 3, 14), new(2027, 2, 28) });
        Assert.Equal(2, result.ExistingCount);
        Assert.Equal(new DateOnly(2027, 3, 21), Assert.Single(result.Occurrences).Date);
    }

    [Theory]
    [InlineData("America/Chicago")]
    [InlineData("Europe/London")]
    [InlineData("Asia/Kolkata")]
    [InlineData("UTC")]
    public void Iana_zones_are_supported_independently_of_host_zone(string zone) => Assert.NotNull(RecurrencePlanner.FindTimeZone(zone));

    [Theory]
    [InlineData("Not/AZone")]
    [InlineData("Central Standard Time")]
    public void Invalid_or_windows_only_zone_names_are_rejected(string zone) =>
        Assert.Throws<ArgumentException>(() => RecurrencePlanner.FindTimeZone(zone));

    private static EventTemplate Template(DateOnly start, TimeOnly time) => new()
    {
        OrganizationId = Guid.NewGuid(), Title = "Weekly gathering", Description = "Test", Location = "Hall",
        DayOfWeek = 0, LocalStartTime = time, DurationMinutes = 90, StartsOn = start, TimeZone = "America/Chicago"
    };
}

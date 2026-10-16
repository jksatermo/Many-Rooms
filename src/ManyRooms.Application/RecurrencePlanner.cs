using System.Globalization;
using ManyRooms.Domain;

namespace ManyRooms.Application;

/// <summary>Weekly wall-clock recurrence with bounded, explicit DST behavior.</summary>
public static class RecurrencePlanner
{
    public static TimeZoneInfo FindTimeZone(string timeZone)
    {
        // ASP.NET runs on Windows and Linux; accept IANA identifiers on both, not machine-local aliases.
        if (timeZone != "UTC" && !TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZone, out _))
            throw new ArgumentException("The organization must use a valid IANA time zone.");
        try { return TimeZoneInfo.FindSystemTimeZoneById(timeZone); }
        catch (TimeZoneNotFoundException) { throw new ArgumentException("The organization time zone is unavailable."); }
        catch (InvalidTimeZoneException) { throw new ArgumentException("The organization time zone is invalid."); }
    }

    public static RecurrencePlan Plan(EventTemplate template, DateOnly throughDate, DateTimeOffset now,
        IReadOnlySet<DateOnly> existingDates)
    {
        var zone = FindTimeZone(template.TimeZone);
        var start = GenerationStart(template, now);
        var days = throughDate.DayNumber - start.DayNumber + 1;
        if (days is < 1 or > 84)
            throw new ArgumentException("Choose an end date covering between 1 and 84 calendar days from the later of the template start and today in the organization time zone.");
        var occurrences = new List<PlannedOccurrence>();
        var skipped = new List<string>();
        var existingCount = 0;
        for (var offset = 0; offset < days; offset++)
        {
            var date = start.AddDays(offset);
            if ((int)date.DayOfWeek != template.DayOfWeek) continue;
            if (existingDates.Contains(date)) { existingCount++; continue; }
            var local = date.ToDateTime(template.LocalStartTime, DateTimeKind.Unspecified);
            if (zone.IsInvalidTime(local)) { skipped.Add(DateText(date)); continue; }
            // The larger offset is the earlier instant in a repeated fall-back hour.
            var utcOffset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
            var begins = new DateTimeOffset(local, utcOffset).ToUniversalTime();
            if (begins <= now) { skipped.Add(DateText(date)); continue; }
            occurrences.Add(new(date, begins, begins.AddMinutes(template.DurationMinutes)));
        }
        return new(occurrences, existingCount, skipped);
    }

    public static DateOnly GenerationStart(EventTemplate template, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, FindTimeZone(template.TimeZone)).DateTime);
        return template.StartsOn > today ? template.StartsOn : today;
    }

    public static string DateText(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

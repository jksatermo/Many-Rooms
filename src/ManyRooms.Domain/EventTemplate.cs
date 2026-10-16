namespace ManyRooms.Domain;

/// <summary>An immutable weekly rule; generated events remain independent scheduling records.</summary>
public sealed class EventTemplate : OrganizationEntity
{
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Location { get; set; }
    public int DayOfWeek { get; set; }
    public TimeOnly LocalStartTime { get; set; }
    public int DurationMinutes { get; set; }
    public DateOnly StartsOn { get; set; }
    public required string TimeZone { get; set; }
}

namespace ManyRooms.Domain;

public sealed class EventStaffingRequirement : OrganizationEntity
{
    public Guid EventId { get; set; }
    public Guid TeamId { get; set; }
    public string? RoleName { get; set; }
    public string NormalizedRoleName { get; set; } = "";
    public int DesiredCount { get; set; }
    public int Version { get; set; } = 1;
}

/// <summary>Assignment-specific immutable history. Names are snapshots, not a live personnel record.</summary>
public sealed class AssignmentHistory : OrganizationEntity
{
    public Guid AssignmentId { get; set; }
    public Guid? ActorMembershipId { get; set; }
    public required string Action { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public int AssignmentVersion { get; set; }
    public string? PreviousVolunteerName { get; set; }
    public required string VolunteerName { get; set; }
    public string? PreviousRoleName { get; set; }
    public required string RoleName { get; set; }

    public static AssignmentHistory Capture(VolunteerAssignment assignment, Guid? actorMembershipId,
        string action, DateTimeOffset now, string? previousVolunteerName = null, string? previousRoleName = null) => new()
        {
            OrganizationId = assignment.OrganizationId,
            AssignmentId = assignment.Id,
            ActorMembershipId = actorMembershipId,
            Action = action,
            OccurredAt = now,
            AssignmentVersion = assignment.Version,
            PreviousVolunteerName = previousVolunteerName,
            VolunteerName = assignment.TeamMember.Membership.User.DisplayName,
            PreviousRoleName = previousRoleName,
            RoleName = assignment.RoleName,
            CreatedAt = now,
            UpdatedAt = now
        };
}

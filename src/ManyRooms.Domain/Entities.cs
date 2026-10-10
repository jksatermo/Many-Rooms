namespace ManyRooms.Domain;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public abstract class OrganizationEntity : Entity
{
    public Guid OrganizationId { get; set; }
}

public sealed class Organization : Entity
{
    public required string Name { get; set; }
    public required string TimeZone { get; set; }
}

public sealed class User : Entity
{
    public required string DisplayName { get; set; }
    public required string Email { get; set; }
}

public sealed class OrganizationMembership : OrganizationEntity
{
    public Guid UserId { get; set; }
    public MembershipRole Role { get; set; }
    public User User { get; set; } = null!;
}

public enum MembershipRole { Volunteer, Leader }

public sealed class Team : OrganizationEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
}

public sealed class TeamMember : OrganizationEntity
{
    public Guid TeamId { get; set; }
    public Guid MembershipId { get; set; }
    public Team Team { get; set; } = null!;
    public OrganizationMembership Membership { get; set; } = null!;
}

public sealed class Event : OrganizationEntity
{
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Location { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
}

public enum AssignmentStatus { Pending, Accepted, Declined }

public sealed class VolunteerAssignment : OrganizationEntity
{
    public Guid EventId { get; set; }
    public Guid TeamMemberId { get; set; }
    public required string RoleName { get; set; }
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Pending;
    public DateTimeOffset? RespondedAt { get; set; }
    public int Version { get; set; } = 1;
    public Event Event { get; set; } = null!;
    public TeamMember TeamMember { get; set; } = null!;

    public void Respond(AssignmentStatus decision, int expectedVersion, DateTimeOffset now)
    {
        if (decision is not (AssignmentStatus.Accepted or AssignmentStatus.Declined))
            throw new ArgumentException("Choose Accepted or Declined.", nameof(decision));
        if (Version != expectedVersion)
            throw new AssignmentConflictException("This assignment changed. Refresh it before responding again.");
        if (Event.StartsAt <= now)
            throw new AssignmentConflictException("Responses close when the event starts. Contact your team leader.");

        Status = decision;
        RespondedAt = now;
        UpdatedAt = now;
        Version++;
    }
}

public sealed class AssignmentResponse : OrganizationEntity
{
    public Guid AssignmentId { get; set; }
    public Guid MembershipId { get; set; }
    public AssignmentStatus Decision { get; set; }
    public int AssignmentVersion { get; set; }
    public DateTimeOffset RespondedAt { get; set; }
}

public sealed class AssignmentConflictException(string message) : Exception(message);

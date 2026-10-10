using ManyRooms.Application;
using ManyRooms.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ManyRooms.Infrastructure;

public sealed class CommunityStore(ManyRoomsDbContext db, TimeProvider clock) : ICommunityQueries, IAssignmentStore,
    IAvailabilityStore, ILeaderAssignmentStore
{
    public Task<bool> IsMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        db.Memberships.AnyAsync(x => x.OrganizationId == organizationId && x.UserId == userId, cancellationToken);

    public Task<bool> IsLeaderAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        db.Memberships.AnyAsync(x => x.OrganizationId == organizationId && x.UserId == userId &&
            x.Role == MembershipRole.Leader, cancellationToken);

    public async Task<DemoContextDto?> GetDemoContextAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var organization = await db.Organizations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == organizationId, cancellationToken);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        var membership = await FindMembershipAsync(organizationId, userId, cancellationToken);
        return organization is null || user is null || membership is null ? null :
            new(organization.Id, organization.Name, user.Id, user.DisplayName, organization.TimeZone, membership.Role.ToString());
    }

    public async Task<IReadOnlyList<EventSummaryDto>> GetEventsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await db.Events.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.StartsAt > now)
            .OrderBy(x => x.StartsAt).Select(x => new EventSummaryDto(x.Id, x.Title, x.StartsAt, x.EndsAt, x.Location,
                db.Assignments.Count(a => a.OrganizationId == organizationId && a.EventId == x.Id),
                db.Assignments.Count(a => a.OrganizationId == organizationId && a.EventId == x.Id && a.Status == AssignmentStatus.Pending)))
            .ToListAsync(cancellationToken);
    }

    public async Task<DashboardDto> GetDashboardAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var organization = await db.Organizations.AsNoTracking().SingleAsync(x => x.Id == organizationId, cancellationToken);
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId, cancellationToken);
        var events = await GetEventsAsync(organizationId, cancellationToken);
        var now = clock.GetUtcNow();
        var assignments = await AssignmentQuery(organizationId).AsNoTracking()
            .Where(x => x.TeamMember.Membership.UserId == userId && x.Event.StartsAt > now)
            .OrderBy(x => x.Event.StartsAt).ToListAsync(cancellationToken);
        var mapped = await MapAssignmentsAsync(organizationId, assignments, userId, false, cancellationToken);
        return new(new(organization.Id, organization.Name, organization.TimeZone), new(user.Id, user.DisplayName),
            new(events.Count, assignments.Count(x => x.Status == AssignmentStatus.Pending), assignments.Count(x => x.Status == AssignmentStatus.Accepted)),
            events, mapped);
    }

    public async Task<EventDetailsDto?> GetEventAsync(Guid organizationId, Guid eventId, Guid userId, CancellationToken cancellationToken)
        => await GetEventDetailsAsync(organizationId, eventId, userId, false, cancellationToken);

    public async Task<EventDetailsDto?> GetLeaderEventAsync(Guid organizationId, Guid eventId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await IsLeaderAsync(organizationId, userId, cancellationToken)) throw new SchedulingAccessException();
        return await GetEventDetailsAsync(organizationId, eventId, userId, true, cancellationToken);
    }

    private async Task<EventDetailsDto?> GetEventDetailsAsync(Guid organizationId, Guid eventId, Guid userId,
        bool showAllConflicts, CancellationToken cancellationToken)
    {
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == eventId, cancellationToken);
        if (item is null) return null;
        var assignments = await AssignmentQuery(organizationId).AsNoTracking().Where(x => x.EventId == eventId)
            .OrderBy(x => x.TeamMember.Team.Name).ThenBy(x => x.RoleName).ToListAsync(cancellationToken);
        return new(item.Id, item.Title, item.Description, item.StartsAt, item.EndsAt, item.Location,
            await MapAssignmentsAsync(organizationId, assignments, userId, showAllConflicts, cancellationToken));
    }

    public async Task<IReadOnlyList<TeamMemberDto>> GetTeamMembersAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await IsLeaderAsync(organizationId, userId, cancellationToken)) throw new SchedulingAccessException();
        return await db.TeamMembers.AsNoTracking().Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Team.Name).ThenBy(x => x.Membership.User.DisplayName)
            .Select(x => new TeamMemberDto(x.Id, x.TeamId, x.Team.Name, x.MembershipId, x.Membership.UserId, x.Membership.User.DisplayName))
            .ToListAsync(cancellationToken);
    }

    public Task<VolunteerAssignment?> FindAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken) =>
        AssignmentQuery(organizationId).SingleOrDefaultAsync(x => x.Id == assignmentId, cancellationToken);

    public async Task SaveResponseAsync(VolunteerAssignment assignment, AssignmentResponse response, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Check the assignment version first. Otherwise EF may insert duplicate history before
            // discovering a competing update. Both saves still commit or roll back together.
            await db.SaveChangesAsync(cancellationToken);
            db.Responses.Add(response);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AssignmentConflictException("This assignment changed. Refresh it before responding again.");
        }
    }

    public async Task<IReadOnlyList<ScheduleConflictDto>> GetConflictsAsync(VolunteerAssignment assignment, CancellationToken cancellationToken)
    {
        var (assignments, unavailable) = await GetConflictCandidatesAsync(assignment.OrganizationId, [assignment], cancellationToken);
        return ScheduleConflictDetector.Detect(assignment, assignments, unavailable);
    }

    private async Task<IReadOnlyList<AssignmentDto>> MapAssignmentsAsync(Guid organizationId,
        IReadOnlyList<VolunteerAssignment> assignments, Guid userId, bool showAllConflicts, CancellationToken cancellationToken)
    {
        var visible = assignments.Where(x => showAllConflicts || x.TeamMember.Membership.UserId == userId).ToArray();
        var (candidates, unavailable) = await GetConflictCandidatesAsync(organizationId, visible, cancellationToken);
        return assignments.Select(x => x.ToDto(userId,
            showAllConflicts || x.TeamMember.Membership.UserId == userId
                ? ScheduleConflictDetector.Detect(x, candidates, unavailable) : [])).ToArray();
    }

    private async Task<(IReadOnlyList<VolunteerAssignment> Assignments, IReadOnlyList<VolunteerUnavailability> Unavailable)>
        GetConflictCandidatesAsync(Guid organizationId, IReadOnlyList<VolunteerAssignment> targets, CancellationToken cancellationToken)
    {
        var active = targets.Where(x => x.Status != AssignmentStatus.Declined).ToArray();
        if (active.Length == 0) return ([], []);
        var memberships = active.Select(x => x.TeamMember.MembershipId).Distinct().ToArray();
        var start = active.Min(x => x.Event.StartsAt);
        var end = active.Max(x => x.Event.EndsAt);
        var candidates = await AssignmentQuery(organizationId).AsNoTracking()
            .Where(x => x.Status != AssignmentStatus.Declined && memberships.Contains(x.TeamMember.MembershipId) &&
                x.Event.StartsAt < end && start < x.Event.EndsAt).ToListAsync(cancellationToken);
        var unavailable = await db.Unavailability.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && memberships.Contains(x.MembershipId) &&
                x.StartsAt < end && start < x.EndsAt).ToListAsync(cancellationToken);
        return (candidates, unavailable);
    }

    public Task<OrganizationMembership?> FindMembershipAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        db.Memberships.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<VolunteerUnavailability>> ListUnavailabilityAsync(Guid organizationId, Guid membershipId, CancellationToken cancellationToken) =>
        await db.Unavailability.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.MembershipId == membershipId)
            .OrderBy(x => x.StartsAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);

    public Task<VolunteerUnavailability?> FindUnavailabilityAsync(Guid organizationId, Guid membershipId, Guid id, CancellationToken cancellationToken) =>
        db.Unavailability.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.MembershipId == membershipId && x.Id == id, cancellationToken);

    public async Task SaveUnavailabilityAsync(VolunteerUnavailability item, bool isNew, CancellationToken cancellationToken)
    {
        if (isNew) db.Unavailability.Add(item);
        await SaveAvailabilityChangesAsync(cancellationToken);
    }

    public async Task DeleteUnavailabilityAsync(VolunteerUnavailability item, CancellationToken cancellationToken)
    {
        db.Unavailability.Remove(item);
        await SaveAvailabilityChangesAsync(cancellationToken);
    }

    private async Task SaveAvailabilityChangesAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            throw new AssignmentConflictException("This unavailable time changed. Refresh before trying again.");
        }
    }

    public Task<Event?> FindEventAsync(Guid organizationId, Guid eventId, CancellationToken cancellationToken) =>
        db.Events.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == eventId, cancellationToken);

    public Task<TeamMember?> FindTeamMemberAsync(Guid organizationId, Guid teamMemberId, CancellationToken cancellationToken) =>
        db.TeamMembers.Include(x => x.Team).Include(x => x.Membership).ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == teamMemberId, cancellationToken);

    public Task<bool> AssignmentExistsAsync(Guid organizationId, Guid eventId, Guid teamMemberId, string roleName, CancellationToken cancellationToken) =>
        db.Assignments.AnyAsync(x => x.OrganizationId == organizationId && x.EventId == eventId &&
            x.TeamMemberId == teamMemberId && x.RoleName == roleName, cancellationToken);

    public async Task SaveAssignmentAsync(VolunteerAssignment assignment, CancellationToken cancellationToken)
    {
        db.Assignments.Add(assignment);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new AssignmentConflictException("This volunteer already has that role for this event.");
        }
    }

    private IQueryable<VolunteerAssignment> AssignmentQuery(Guid organizationId) =>
        db.Assignments.Where(x => x.OrganizationId == organizationId).Include(x => x.Event)
            .Include(x => x.TeamMember).ThenInclude(x => x.Team)
            .Include(x => x.TeamMember).ThenInclude(x => x.Membership).ThenInclude(x => x.User);
}

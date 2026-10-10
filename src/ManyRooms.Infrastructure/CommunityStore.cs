using ManyRooms.Application;
using ManyRooms.Domain;
using Microsoft.EntityFrameworkCore;

namespace ManyRooms.Infrastructure;

public sealed class CommunityStore(ManyRoomsDbContext db, TimeProvider clock) : ICommunityQueries, IAssignmentStore
{
    public Task<bool> IsMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        db.Memberships.AnyAsync(x => x.OrganizationId == organizationId && x.UserId == userId, cancellationToken);

    public async Task<DemoContextDto?> GetDemoContextAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var organization = await db.Organizations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == organizationId, cancellationToken);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        return organization is null || user is null ? null : new(organization.Id, organization.Name, user.Id, user.DisplayName, organization.TimeZone);
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
        return new(new(organization.Id, organization.Name, organization.TimeZone), new(user.Id, user.DisplayName),
            new(events.Count, assignments.Count(x => x.Status == AssignmentStatus.Pending), assignments.Count(x => x.Status == AssignmentStatus.Accepted)),
            events, assignments.Select(x => x.ToDto(userId)).ToList());
    }

    public async Task<EventDetailsDto?> GetEventAsync(Guid organizationId, Guid eventId, Guid userId, CancellationToken cancellationToken)
    {
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == eventId, cancellationToken);
        if (item is null) return null;
        var assignments = await AssignmentQuery(organizationId).AsNoTracking().Where(x => x.EventId == eventId)
            .OrderBy(x => x.TeamMember.Team.Name).ThenBy(x => x.RoleName).ToListAsync(cancellationToken);
        return new(item.Id, item.Title, item.Description, item.StartsAt, item.EndsAt, item.Location,
            assignments.Select(x => x.ToDto(userId)).ToList());
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

    private IQueryable<VolunteerAssignment> AssignmentQuery(Guid organizationId) =>
        db.Assignments.Where(x => x.OrganizationId == organizationId).Include(x => x.Event)
            .Include(x => x.TeamMember).ThenInclude(x => x.Team)
            .Include(x => x.TeamMember).ThenInclude(x => x.Membership).ThenInclude(x => x.User);
}

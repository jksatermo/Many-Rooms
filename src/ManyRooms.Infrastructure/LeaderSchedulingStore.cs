using ManyRooms.Application;
using ManyRooms.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ManyRooms.Infrastructure;

public sealed partial class CommunityStore
{
    // All leader schedule writes take the same transaction-scoped org lock. This closes
    // the check-then-save race between membership removal and assignment selection.
    private Task LockOrganizationAsync(Guid organizationId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({organizationId.ToString()}, 481730))", ct);

    public async Task<T> InOrganizationTransactionAsync<T>(Guid organizationId, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockOrganizationAsync(organizationId, cancellationToken);
        try
        {
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AssignmentConflictException("This schedule item changed. Refresh before trying again.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new AssignmentConflictException("That schedule item already exists. Refresh before trying again.");
        }
    }

    public async Task<IReadOnlyList<Team>> ListTeamsAsync(Guid organizationId, CancellationToken ct) =>
        await db.Teams.Where(x => x.OrganizationId == organizationId).ToListAsync(ct);
    public async Task<IReadOnlyList<TeamMember>> ListMembersAsync(Guid organizationId, CancellationToken ct) =>
        await db.TeamMembers.Where(x => x.OrganizationId == organizationId).Include(x => x.Team)
            .Include(x => x.Membership).ThenInclude(x => x.User).ToListAsync(ct);
    public async Task<IReadOnlyList<OrganizationMembership>> ListMembershipsAsync(Guid organizationId, CancellationToken ct) =>
        await db.Memberships.Where(x => x.OrganizationId == organizationId).Include(x => x.User).ToListAsync(ct);
    public async Task<IReadOnlyList<VolunteerAssignment>> ListAssignmentsAsync(Guid organizationId, CancellationToken ct) =>
        await AssignmentQuery(organizationId).ToListAsync(ct);
    public async Task<IReadOnlyList<EventStaffingRequirement>> ListStaffingAsync(Guid organizationId, Guid eventId, CancellationToken ct) =>
        await db.Set<EventStaffingRequirement>().Where(x => x.OrganizationId == organizationId && x.EventId == eventId).ToListAsync(ct);
    public async Task<IReadOnlyList<VolunteerUnavailability>> ListUnavailableAsync(Guid organizationId, CancellationToken ct) =>
        await db.Unavailability.AsNoTracking().Where(x => x.OrganizationId == organizationId).ToListAsync(ct);

    public async Task<IReadOnlyList<AssignmentHistoryDto>> ListHistoryAsync(Guid organizationId, Guid? eventId, Guid? assignmentId, CancellationToken ct)
    {
        var history = db.Set<AssignmentHistory>().AsNoTracking().Where(x => x.OrganizationId == organizationId);
        if (assignmentId.HasValue) history = history.Where(x => x.AssignmentId == assignmentId.Value);
        if (eventId.HasValue) history = history.Where(x => db.Assignments.Any(a => a.OrganizationId == organizationId && a.Id == x.AssignmentId && a.EventId == eventId.Value));
        return await history.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.AssignmentVersion).ThenBy(x => x.Id)
            .Select(x => new AssignmentHistoryDto(x.Id, x.AssignmentId, x.Action, x.OccurredAt,
                db.Memberships.Where(m => m.OrganizationId == organizationId && m.Id == x.ActorMembershipId)
                    .Select(m => m.User.DisplayName).SingleOrDefault(),
                x.PreviousVolunteerName, x.VolunteerName, x.PreviousRoleName, x.RoleName, x.AssignmentVersion)).ToListAsync(ct);
    }

    public async Task SaveSchedulingAsync(OrganizationEntity entity, bool isNew, AssignmentHistory? history, CancellationToken ct)
    {
        if (isNew) db.Add(entity);
        // Persist the version check before inserting immutable history, under the same transaction.
        await db.SaveChangesAsync(ct);
        if (history is not null)
        {
            db.Set<AssignmentHistory>().Add(history);
            await db.SaveChangesAsync(ct);
        }
    }
    public async Task DeleteStaffingAsync(EventStaffingRequirement entity, CancellationToken ct)
    {
        db.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}

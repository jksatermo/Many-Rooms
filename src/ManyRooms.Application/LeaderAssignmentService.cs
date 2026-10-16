using ManyRooms.Domain;

namespace ManyRooms.Application;

public sealed class LeaderAssignmentService(ILeaderAssignmentStore store, TimeProvider clock)
{
    public async Task<AssignmentPreviewDto> PreviewAsync(Guid organizationId, Guid eventId, Guid userId,
        CreateAssignmentRequest request, CancellationToken cancellationToken)
    {
        var assignment = await PrepareAsync(organizationId, eventId, userId, request, cancellationToken);
        return new(await store.GetConflictsAsync(assignment, cancellationToken));
    }

    public async Task<AssignmentDto> CreateAsync(Guid organizationId, Guid eventId, Guid userId,
        CreateAssignmentRequest request, CancellationToken cancellationToken)
    {
        return await store.InOrganizationTransactionAsync(organizationId, async () =>
        {
            var assignment = await PrepareAsync(organizationId, eventId, userId, request, cancellationToken);
            await store.SaveAssignmentAsync(assignment, userId, cancellationToken);
            return assignment.ToDto(userId, await store.GetConflictsAsync(assignment, cancellationToken));
        }, cancellationToken);
    }

    private async Task<VolunteerAssignment> PrepareAsync(Guid organizationId, Guid eventId, Guid userId,
        CreateAssignmentRequest request, CancellationToken cancellationToken)
    {
        // Authorization is repeated at the use-case boundary so future clients cannot bypass it.
        if (!await store.IsLeaderAsync(organizationId, userId, cancellationToken)) throw new SchedulingAccessException();
        var role = request.RoleName?.Trim();
        if (string.IsNullOrEmpty(role) || role.Length > 120)
            throw new ArgumentException("RoleName must contain between 1 and 120 characters.");
        var item = await store.FindEventAsync(organizationId, eventId, cancellationToken) ?? throw new SchedulingNotFoundException();
        var member = await store.FindTeamMemberAsync(organizationId, request.TeamMemberId, cancellationToken) ?? throw new SchedulingNotFoundException();
        var now = clock.GetUtcNow();
        if (item.StartsAt <= now) throw new AssignmentConflictException("Assignments cannot be added after the event starts.");
        if (await store.AssignmentExistsAsync(organizationId, eventId, member.Id, role, cancellationToken))
            throw new AssignmentConflictException("This volunteer already has that role for this event.");
        return new()
        {
            OrganizationId = organizationId,
            EventId = item.Id,
            Event = item,
            TeamMemberId = member.Id,
            TeamMember = member,
            RoleName = role,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}

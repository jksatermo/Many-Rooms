using ManyRooms.Domain;

namespace ManyRooms.Application;

public sealed class AssignmentResponseService(IAssignmentStore store, TimeProvider clock)
{
    public async Task<AssignmentDto> RespondAsync(Guid organizationId, Guid assignmentId, Guid userId,
        RespondToAssignmentRequest request, CancellationToken cancellationToken)
    {
        // Membership is checked here too so future clients (including SMS) share this boundary.
        if (!await store.IsMemberAsync(organizationId, userId, cancellationToken))
            throw new AssignmentAccessException();
        var decision = request.Decision switch
        {
            "Accepted" => AssignmentStatus.Accepted,
            "Declined" => AssignmentStatus.Declined,
            _ => throw new ArgumentException("Decision must be Accepted or Declined.")
        };
        if (request.ExpectedVersion < 1)
            throw new ArgumentException("ExpectedVersion must be a positive integer.");

        var assignment = await store.FindAsync(organizationId, assignmentId, cancellationToken)
            ?? throw new AssignmentNotFoundException();
        if (assignment.TeamMember.Membership.UserId != userId)
            throw new AssignmentAccessException();

        var now = clock.GetUtcNow();
        assignment.Respond(decision, request.ExpectedVersion, now);
        await store.SaveResponseAsync(assignment, new AssignmentResponse
        {
            OrganizationId = organizationId,
            AssignmentId = assignment.Id,
            MembershipId = assignment.TeamMember.MembershipId,
            Decision = decision,
            AssignmentVersion = assignment.Version,
            RespondedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);
        return assignment.ToDto(userId);
    }
}

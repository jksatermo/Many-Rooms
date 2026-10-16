using System.Security.Claims;
using ManyRooms.Application;

namespace ManyRooms.Api;

public static class LeaderSchedulingEndpoints
{
    public static void MapLeaderSchedulingEndpoints(this RouteGroupBuilder leader)
    {
        leader.MapGet("/teams", async (Guid organizationId, HttpContext context, LeaderSchedulingService service, CancellationToken ct) =>
            Results.Ok(await service.TeamsAsync(organizationId, User(context), ct))).Produces<IReadOnlyList<TeamSummaryDto>>();
        leader.MapGet("/teams/{teamId:guid}", async (Guid organizationId, Guid teamId, HttpContext context, LeaderSchedulingService service, CancellationToken ct) =>
            Results.Ok(await service.TeamAsync(organizationId, teamId, User(context), ct))).Produces<TeamDetailsDto>();
        leader.MapGet("/organization-members", async (Guid organizationId, HttpContext context, LeaderSchedulingService service, CancellationToken ct) =>
            Results.Ok(await service.OrganizationMembersAsync(organizationId, User(context), ct))).Produces<IReadOnlyList<OrganizationMemberDto>>();
        leader.MapPost("/teams/{teamId:guid}/members", async (Guid organizationId, Guid teamId, AddTeamMemberRequest request,
            HttpContext context, LeaderSchedulingService service, CancellationToken ct) =>
            Results.Json(await service.AddMemberAsync(organizationId, teamId, User(context), request, ct), statusCode: 201)).Produces<TeamMemberDto>(201);
        leader.MapPut("/teams/{teamId:guid}/members/{memberId:guid}", async (Guid organizationId, Guid teamId, Guid memberId, UpdateTeamMemberRequest request,
            HttpContext context, LeaderSchedulingService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateMemberAsync(organizationId, teamId, memberId, User(context), request, ct))).Produces<TeamMemberDto>();
        leader.MapDelete("/teams/{teamId:guid}/members/{memberId:guid}", async (Guid organizationId, Guid teamId, Guid memberId, int expectedVersion,
            HttpContext context, LeaderSchedulingService service, CancellationToken ct) =>
        {
            await service.RemoveMemberAsync(organizationId, teamId, memberId, User(context), expectedVersion, ct);
            return Results.NoContent();
        }).Produces(204);
        leader.MapGet("/events/{eventId:guid}/staffing", async (Guid organizationId, Guid eventId, HttpContext context, LeaderSchedulingService service, CancellationToken ct) =>
            Results.Ok(await service.StaffingAsync(organizationId, eventId, User(context), ct))).Produces<IReadOnlyList<StaffingRequirementDto>>();
        leader.MapPost("/events/{eventId:guid}/staffing", async (Guid organizationId, Guid eventId, CreateStaffingRequest request,
            HttpContext context, LeaderSchedulingService service, CancellationToken ct) =>
            Results.Json(await service.CreateStaffingAsync(organizationId, eventId, User(context), request, ct), statusCode: 201)).Produces<StaffingRequirementDto>(201);
        leader.MapPut("/events/{eventId:guid}/staffing/{id:guid}", async (Guid organizationId, Guid eventId, Guid id, UpdateStaffingRequest request,
            HttpContext context, LeaderSchedulingService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateStaffingAsync(organizationId, eventId, id, User(context), request, ct))).Produces<StaffingRequirementDto>();
        leader.MapDelete("/events/{eventId:guid}/staffing/{id:guid}", async (Guid organizationId, Guid eventId, Guid id, int expectedVersion,
            HttpContext context, LeaderSchedulingService service, CancellationToken ct) =>
        {
            await service.RemoveStaffingAsync(organizationId, eventId, id, User(context), expectedVersion, ct);
            return Results.NoContent();
        }).Produces(204);
        leader.MapPost("/assignments/{id:guid}/preview", async (Guid organizationId, Guid id, UpdateAssignmentRequest request,
            HttpContext context, LeaderSchedulingService service, CancellationToken ct) =>
            Results.Ok(await service.PreviewEditAsync(organizationId, id, User(context), request, ct))).Produces<AssignmentPreviewDto>();
        leader.MapPut("/assignments/{id:guid}", async (Guid organizationId, Guid id, UpdateAssignmentRequest request,
            HttpContext context, LeaderSchedulingService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAssignmentAsync(organizationId, id, User(context), request, ct))).Produces<AssignmentDto>();
        leader.MapDelete("/assignments/{id:guid}", async (Guid organizationId, Guid id, int expectedVersion,
            HttpContext context, LeaderSchedulingService service, CancellationToken ct) =>
            Results.Ok(await service.CancelAsync(organizationId, id, User(context), expectedVersion, ct))).Produces<AssignmentDto>();
        leader.MapGet("/events/{eventId:guid}/history", async (Guid organizationId, Guid eventId,
            HttpContext context, LeaderSchedulingService service, CancellationToken ct) =>
            Results.Ok(await service.HistoryAsync(organizationId, User(context), eventId, null, ct))).Produces<IReadOnlyList<AssignmentHistoryDto>>();
        leader.MapGet("/assignments/{id:guid}/history", async (Guid organizationId, Guid id,
            HttpContext context, LeaderSchedulingService service, CancellationToken ct) =>
            Results.Ok(await service.HistoryAsync(organizationId, User(context), null, id, ct))).Produces<IReadOnlyList<AssignmentHistoryDto>>();
        leader.MapGet("/conflicts", async (Guid organizationId, HttpContext context, LeaderSchedulingService service, CancellationToken ct) =>
            Results.Ok(await service.ConflictsAsync(organizationId, User(context), ct))).Produces<IReadOnlyList<AssignmentDto>>();
        leader.MapGet("/assignments/{id:guid}/replacement-candidates", async (Guid organizationId, Guid id,
            HttpContext context, LeaderSchedulingService service, CancellationToken ct) =>
            Results.Ok(await service.ReplacementsAsync(organizationId, id, User(context), ct))).Produces<IReadOnlyList<ReplacementCandidateDto>>();
    }
    private static Guid User(HttpContext context) => Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

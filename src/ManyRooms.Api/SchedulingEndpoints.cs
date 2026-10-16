using System.Security.Claims;
using ManyRooms.Application;

namespace ManyRooms.Api;

public static class SchedulingEndpoints
{
    public static void MapSchedulingEndpoints(this RouteGroupBuilder organization)
    {
        organization.MapGet("/availability", async (Guid organizationId, HttpContext context,
            AvailabilityService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(organizationId, CurrentUser(context), cancellationToken)))
            .Produces<IReadOnlyList<UnavailabilityDto>>().WithSummary("List your unavailable time ranges");

        organization.MapGet("/availability/{id:guid}", async (Guid organizationId, Guid id, HttpContext context,
            AvailabilityService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAsync(organizationId, id, CurrentUser(context), cancellationToken)))
            .Produces<UnavailabilityDto>().ProducesProblem(404).WithSummary("Get one of your unavailable time ranges");

        organization.MapPost("/availability", async (Guid organizationId, CreateUnavailabilityRequest request,
            HttpContext context, AvailabilityService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(organizationId, CurrentUser(context), request, cancellationToken);
            return Results.Created($"/api/organizations/{organizationId}/availability/{result.Id}", result);
        }).Produces<UnavailabilityDto>(201).ProducesProblem(400)
            .WithSummary("Add your unavailable time with an optional private note");

        organization.MapPut("/availability/{id:guid}", async (Guid organizationId, Guid id, UpdateUnavailabilityRequest request,
            HttpContext context, AvailabilityService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.UpdateAsync(organizationId, id, CurrentUser(context), request, cancellationToken)))
            .Produces<UnavailabilityDto>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409)
            .WithSummary("Update your unavailable time using its current version");

        organization.MapDelete("/availability/{id:guid}", async (Guid organizationId, Guid id, int expectedVersion,
            HttpContext context, AvailabilityService service, CancellationToken cancellationToken) =>
        {
            await service.DeleteAsync(organizationId, id, CurrentUser(context), expectedVersion, cancellationToken);
            return Results.NoContent();
        }).Produces(204).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409)
            .WithSummary("Remove your unavailable time using its current version");

        var leader = organization.MapGroup("/leader");
        leader.AddEndpointFilter(async (context, next) =>
        {
            var organizationId = Guid.Parse(context.HttpContext.Request.RouteValues["organizationId"]!.ToString()!);
            var queries = context.HttpContext.RequestServices.GetRequiredService<ICommunityQueries>();
            return await queries.IsLeaderAsync(organizationId, CurrentUser(context.HttpContext), context.HttpContext.RequestAborted)
                ? await next(context)
                : Results.Problem(statusCode: 403, title: "Leader access required", detail: "A leader membership is required in this organization.");
        });
        leader.MapLeaderSchedulingEndpoints();
        leader.MapRecurrenceEndpoints();
        leader.MapGet("/events/{eventId:guid}", async (Guid organizationId, Guid eventId, HttpContext context,
            ICommunityQueries queries, CancellationToken cancellationToken) =>
        {
            var result = await queries.GetLeaderEventAsync(organizationId, eventId, CurrentUser(context), cancellationToken);
            return result is null ? Results.Problem(statusCode: 404, title: "Event not found") : Results.Ok(result);
        }).Produces<EventDetailsDto>().ProducesProblem(403).ProducesProblem(404)
            .WithSummary("Review event assignments and advisory conflicts as a leader");

        leader.MapGet("/team-members", async (Guid organizationId, HttpContext context,
            ICommunityQueries queries, CancellationToken cancellationToken) =>
            Results.Ok(await queries.GetTeamMembersAsync(organizationId, CurrentUser(context), cancellationToken)))
            .Produces<IReadOnlyList<TeamMemberDto>>().ProducesProblem(403)
            .WithSummary("List existing organization team members for assignment selection");

        leader.MapPost("/events/{eventId:guid}/assignments/preview", async (Guid organizationId, Guid eventId,
            CreateAssignmentRequest request, HttpContext context, LeaderAssignmentService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.PreviewAsync(organizationId, eventId, CurrentUser(context), request, cancellationToken)))
            .Produces<AssignmentPreviewDto>().ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409)
            .WithSummary("Preview conflicts for a proposed assignment without saving it");

        leader.MapPost("/events/{eventId:guid}/assignments", async (Guid organizationId, Guid eventId,
            CreateAssignmentRequest request, HttpContext context, LeaderAssignmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(organizationId, eventId, CurrentUser(context), request, cancellationToken);
            return Results.Json(result, statusCode: StatusCodes.Status201Created);
        }).Produces<AssignmentDto>(201).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409)
            .WithSummary("Create an assignment and return current advisory conflicts");
    }

    private static Guid CurrentUser(HttpContext context) => Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

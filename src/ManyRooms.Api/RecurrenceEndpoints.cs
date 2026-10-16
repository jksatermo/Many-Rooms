using System.Security.Claims;
using ManyRooms.Application;

namespace ManyRooms.Api;

public static class RecurrenceEndpoints
{
    public static void MapRecurrenceEndpoints(this RouteGroupBuilder leader)
    {
        leader.MapGet("/event-templates", async (Guid organizationId, HttpContext context,
            RecurrenceService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(organizationId, CurrentUser(context), cancellationToken)))
            .Produces<IReadOnlyList<EventTemplateDto>>().ProducesProblem(403)
            .WithSummary("List weekly event templates in the organization time zone");

        leader.MapPost("/event-templates", async (Guid organizationId, CreateEventTemplateRequest request,
            HttpContext context, RecurrenceService service, CancellationToken cancellationToken) =>
            Results.Json(await service.CreateTemplateAsync(organizationId, CurrentUser(context), request, cancellationToken), statusCode: 201))
            .Produces<EventTemplateDto>(201).ProducesProblem(400).ProducesProblem(403)
            .WithSummary("Create an immutable weekly event template");

        leader.MapPost("/event-templates/{id:guid}/generate", async (Guid organizationId, Guid id, GenerateEventsRequest request,
            HttpContext context, RecurrenceService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GenerateAsync(organizationId, id, CurrentUser(context), request, cancellationToken)))
            .Produces<GeneratedEventsDto>().ProducesProblem(400).ProducesProblem(403).ProducesProblem(404)
            .WithSummary("Generate at most 12 weeks of real event instances without duplicates");

        leader.MapPost("/events", async (Guid organizationId, CreateEventRequest request,
            HttpContext context, RecurrenceService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateEventAsync(organizationId, CurrentUser(context), request, cancellationToken);
            return Results.Created($"/api/organizations/{organizationId}/leader/events/{result.Id}", result);
        }).Produces<EventDetailsDto>(201).ProducesProblem(400).ProducesProblem(403)
            .WithSummary("Create a future one-time event with explicit UTC offsets");
    }

    private static Guid CurrentUser(HttpContext context) => Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

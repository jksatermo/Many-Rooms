using System.Security.Claims;
using ManyRooms.Api;
using ManyRooms.Application;
using ManyRooms.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var demoEnabled = builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Demo:Enabled");
if (!builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Demo:Enabled"))
    throw new InvalidOperationException("The demo identity may only be enabled in Development.");
var connectionString = builder.Configuration.GetConnectionString("ManyRooms")
    ?? throw new InvalidOperationException("Set ConnectionStrings__ManyRooms. See the README local setup instructions.");
builder.Services.AddDbContext<ManyRoomsDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CommunityStore>();
builder.Services.AddScoped<ICommunityQueries>(provider => provider.GetRequiredService<CommunityStore>());
builder.Services.AddScoped<IAssignmentStore>(provider => provider.GetRequiredService<CommunityStore>());
builder.Services.AddScoped<IAvailabilityStore>(provider => provider.GetRequiredService<CommunityStore>());
builder.Services.AddScoped<ILeaderAssignmentStore>(provider => provider.GetRequiredService<CommunityStore>());
builder.Services.AddScoped<AssignmentResponseService>();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<LeaderAssignmentService>();
if (demoEnabled) builder.Services.AddScoped<ISmsProvider, ConsoleSmsProvider>();
builder.Services.AddAuthentication(DemoAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(DemoAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .WithMethods("GET", "POST", "PUT", "DELETE").WithHeaders("Content-Type", "X-ManyRooms-Demo")));
var app = builder.Build();

if (args.Contains("--seed"))
{
    if (!demoEnabled) throw new InvalidOperationException("Demo seed requires Development and Demo:Enabled=true.");
    await using var scope = app.Services.CreateAsyncScope();
    await DemoData.SeedAsync(scope.ServiceProvider.GetRequiredService<ManyRoomsDbContext>(), TimeProvider.System);
    app.Logger.LogInformation("Fictional demo seed is ready. Existing responses are preserved.");
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages(async status =>
{
    await Results.Problem(statusCode: status.HttpContext.Response.StatusCode).ExecuteAsync(status.HttpContext);
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Many Rooms development demo API v1");
        // Swagger is development-only, and its requests opt into the same fixed demo identity as the clients.
        options.UseRequestInterceptor("(request) => { request.headers['X-ManyRooms-Demo'] = 'true'; return request; }");
    });
}
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapGet("/health/ready", async (ManyRoomsDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.Problem(statusCode: 503, title: "Database unavailable")).AllowAnonymous();
app.MapGet("/api/demo/context", async (string? persona, ICommunityQueries queries, CancellationToken cancellationToken) =>
{
    if (!demoEnabled) return Results.NotFound();
    if (persona is not (null or "volunteer" or "leader"))
        return Results.Problem(statusCode: 400, title: "Invalid demo persona", detail: "Choose volunteer or leader.");
    var context = await queries.GetDemoContextAsync(DemoData.OrganizationId,
        persona == "leader" ? DemoData.LeaderUserId : DemoData.UserId, cancellationToken);
    return context is null ? Results.Problem(statusCode: 503, title: "Demo is not seeded", detail: "Run the documented database migration and seed commands.") : Results.Ok(context);
}).AllowAnonymous().Produces<DemoContextDto>().ProducesProblem(503)
    .WithSummary("Get the explicitly shared, fictional development demo context");

var organization = app.MapGroup("/api/organizations/{organizationId:guid}").RequireAuthorization();
organization.AddEndpointFilter(async (context, next) =>
{
    var organizationId = Guid.Parse(context.HttpContext.Request.RouteValues["organizationId"]!.ToString()!);
    var queries = context.HttpContext.RequestServices.GetRequiredService<ICommunityQueries>();
    return await queries.IsMemberAsync(organizationId, CurrentUser(context.HttpContext), context.HttpContext.RequestAborted)
        ? await next(context)
        : Results.Problem(statusCode: 403, title: "Access denied", detail: "You are not a member of this organization.");
});
organization.MapGet("/dashboard", async (Guid organizationId, HttpContext context, ICommunityQueries queries, CancellationToken cancellationToken) =>
    Results.Ok(await queries.GetDashboardAsync(organizationId, CurrentUser(context), cancellationToken)))
    .Produces<DashboardDto>().WithSummary("Get upcoming events and the current volunteer's assignments");
organization.MapGet("/events", async (Guid organizationId, ICommunityQueries queries, CancellationToken cancellationToken) =>
    Results.Ok(await queries.GetEventsAsync(organizationId, cancellationToken)))
    .Produces<IReadOnlyList<EventSummaryDto>>().WithSummary("List upcoming organization events");
organization.MapGet("/events/{eventId:guid}", async (Guid organizationId, Guid eventId, HttpContext context, ICommunityQueries queries, CancellationToken cancellationToken) =>
{
    var item = await queries.GetEventAsync(organizationId, eventId, CurrentUser(context), cancellationToken);
    return item is null ? Results.Problem(statusCode: 404, title: "Event not found") : Results.Ok(item);
}).Produces<EventDetailsDto>().ProducesProblem(404).WithSummary("Get an event and its team assignments");
organization.MapPut("/assignments/{assignmentId:guid}/response", async (Guid organizationId, Guid assignmentId,
    RespondToAssignmentRequest request, HttpContext context, AssignmentResponseService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.RespondAsync(organizationId, assignmentId, CurrentUser(context), request, cancellationToken)))
    .Produces<AssignmentDto>().ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409)
    .WithSummary("Accept or decline your assignment before its event starts")
    .WithDescription("Development demo requests use X-ManyRooms-Demo: true (Swagger supplies it). Send the current assignment version; a stale response returns 409. Production authentication is not implemented.");
organization.MapSchedulingEndpoints();
await app.RunAsync();

static Guid CurrentUser(HttpContext context) => Guid.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
public partial class Program;

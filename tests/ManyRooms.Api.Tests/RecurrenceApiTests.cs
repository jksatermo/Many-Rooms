using System.Net;
using System.Net.Http.Json;
using ManyRooms.Application;
using ManyRooms.Domain;
using ManyRooms.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ManyRooms.Api.Tests;

public sealed class RecurrenceApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static string Leader => $"/api/organizations/{DemoData.OrganizationId}/leader";
    private static DateOnly Tomorrow => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
    private static CreateEventTemplateRequest TemplateRequest => new("  Weekly welcome  ", "  A shared morning  ", "  Main hall  ",
        (int)Tomorrow.DayOfWeek, "10:00", 90, RecurrencePlanner.DateText(Tomorrow));

    [Fact]
    public async Task Template_and_one_time_event_endpoints_require_leader_membership()
    {
        using var anonymous = fixture.CreateClient(false);
        using var volunteer = fixture.CreateClient();
        using var leader = fixture.CreateLeaderClient();
        using var noIdentity = await anonymous.GetAsync($"{Leader}/event-templates");
        Assert.Equal(HttpStatusCode.Unauthorized, noIdentity.StatusCode);
        using var read = await volunteer.GetAsync($"{Leader}/event-templates");
        using var create = await volunteer.PostAsJsonAsync($"{Leader}/event-templates", TemplateRequest);
        using var generate = await volunteer.PostAsJsonAsync($"{Leader}/event-templates/{RecurrenceSeed.TemplateId}/generate", new { throughDate = TemplateRequest.StartsOn });
        using var oneTime = await volunteer.PostAsJsonAsync($"{Leader}/events", EventRequest());
        Assert.All(new[] { read, create, generate, oneTime }, response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
        using var crossOrg = await leader.GetAsync($"/api/organizations/{DemoData.OtherOrganizationId}/leader/event-templates");
        Assert.Equal(HttpStatusCode.Forbidden, crossOrg.StatusCode);
    }

    [Fact]
    public async Task Weekly_generation_persists_instances_is_repeatable_and_keeps_existing_records_unchanged()
    {
        using var client = fixture.CreateLeaderClient();
        var template = await CreateTemplate(client);
        Assert.Equal("Weekly welcome", template.Title);
        Assert.Equal("America/Chicago", template.TimeZone);
        var request = new GenerateEventsRequest(RecurrencePlanner.DateText(Tomorrow.AddDays(27)));
        using var first = await client.PostAsJsonAsync($"{Leader}/event-templates/{template.Id}/generate", request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = (await first.Content.ReadFromJsonAsync<GeneratedEventsDto>())!;
        Assert.Equal(4, firstBody.CreatedEvents.Count);
        Assert.Equal(0, firstBody.ExistingCount);
        await using var db = fixture.CreateDbContext();
        var firstEvent = await db.Events.SingleAsync(x => x.Id == firstBody.CreatedEvents[0].Id);
        firstEvent.Description = "Leader-maintained event details";
        await db.SaveChangesAsync();
        using var repeat = await client.PostAsJsonAsync($"{Leader}/event-templates/{template.Id}/generate", request);
        var repeatBody = (await repeat.Content.ReadFromJsonAsync<GeneratedEventsDto>())!;
        Assert.Empty(repeatBody.CreatedEvents);
        Assert.Equal(4, repeatBody.ExistingCount);
        Assert.Equal(4, await db.Events.CountAsync(x => x.TemplateId == template.Id));
        Assert.Equal("Leader-maintained event details", (await db.Events.AsNoTracking().SingleAsync(x => x.Id == firstEvent.Id)).Description);
        foreach (var item in firstBody.CreatedEvents)
        {
            var details = await client.GetFromJsonAsync<EventDetailsDto>($"{Leader}/events/{item.Id}");
            Assert.NotNull(details);
            Assert.Empty(details.Assignments);
        }
    }

    [Fact]
    public async Task Simultaneous_generation_requests_do_not_duplicate_occurrences()
    {
        using var firstClient = fixture.CreateLeaderClient();
        using var secondClient = fixture.CreateLeaderClient();
        var template = await CreateTemplate(firstClient);
        var request = new GenerateEventsRequest(RecurrencePlanner.DateText(Tomorrow.AddDays(27)));
        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync($"{Leader}/event-templates/{template.Id}/generate", request),
            secondClient.PostAsJsonAsync($"{Leader}/event-templates/{template.Id}/generate", request));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            var bodies = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<GeneratedEventsDto>()));
            Assert.Equal(4, bodies.Sum(body => body!.CreatedEvents.Count));
            Assert.Equal(4, bodies.Sum(body => body!.ExistingCount));
            await using var db = fixture.CreateDbContext();
            var dates = await db.Events.Where(x => x.TemplateId == template.Id).Select(x => x.OccurrenceDate).ToListAsync();
            Assert.Equal(4, dates.Count);
            Assert.Equal(4, dates.Distinct().Count());
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Foreign_templates_are_hidden_and_database_rejects_cross_organization_occurrences()
    {
        await using var db = fixture.CreateDbContext();
        var foreign = new EventTemplate
        {
            OrganizationId = DemoData.OtherOrganizationId, Title = "Foreign weekly event", Description = "Test", Location = "Hall",
            DayOfWeek = (int)Tomorrow.DayOfWeek, LocalStartTime = new(10, 0), DurationMinutes = 60,
            StartsOn = Tomorrow, TimeZone = "America/Chicago"
        };
        db.Add(foreign);
        await db.SaveChangesAsync();
        using var client = fixture.CreateLeaderClient();
        var list = await client.GetFromJsonAsync<EventTemplateDto[]>($"{Leader}/event-templates");
        Assert.DoesNotContain(list!, item => item.Id == foreign.Id);
        using var generate = await client.PostAsJsonAsync($"{Leader}/event-templates/{foreign.Id}/generate", new GenerateEventsRequest(TemplateRequest.StartsOn));
        Assert.Equal(HttpStatusCode.NotFound, generate.StatusCode);
        db.Events.Add(new Event
        {
            OrganizationId = DemoData.OrganizationId, TemplateId = foreign.Id, OccurrenceDate = Tomorrow,
            Title = "Invalid foreign reference", Description = "Test", Location = "Hall",
            StartsAt = DateTimeOffset.UtcNow.AddDays(2), EndsAt = DateTimeOffset.UtcNow.AddDays(2).AddHours(1)
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Invalid_template_fields_and_unbounded_or_invalid_generation_dates_are_rejected()
    {
        using var client = fixture.CreateLeaderClient();
        var valid = TemplateRequest;
        CreateEventTemplateRequest[] invalid = [valid with { Title = " " }, valid with { Title = new string('x', 161) },
            valid with { Location = "" }, valid with { Description = new string('x', 4001) }, valid with { DayOfWeek = 7 },
            valid with { DayOfWeek = -1 }, valid with { LocalStartTime = "25:30" }, valid with { LocalStartTime = "10:00:00" },
            valid with { DurationMinutes = 14 }, valid with { DurationMinutes = 721 }, valid with { StartsOn = "2027-02-30" }];
        foreach (var request in invalid)
        {
            using var response = await client.PostAsJsonAsync($"{Leader}/event-templates", request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        var template = await CreateTemplate(client);
        foreach (var end in new[] { "bad-date", RecurrencePlanner.DateText(Tomorrow.AddDays(-1)), RecurrencePlanner.DateText(Tomorrow.AddDays(84)) })
        {
            using var response = await client.PostAsJsonAsync($"{Leader}/event-templates/{template.Id}/generate", new GenerateEventsRequest(end));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        await using var db = fixture.CreateDbContext();
        Assert.False(await db.Events.AnyAsync(item => item.TemplateId == template.Id));
    }

    [Fact]
    public async Task One_time_event_normalizes_offsets_and_is_available_through_existing_event_read()
    {
        using var client = fixture.CreateLeaderClient();
        var request = EventRequest();
        using var response = await client.PostAsJsonAsync($"{Leader}/events", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<EventDetailsDto>())!;
        Assert.Equal(TimeSpan.Zero, result.StartsAt.Offset);
        Assert.Equal(DateTimeOffset.Parse(request.StartsAt!), result.StartsAt);
        Assert.Empty(result.Assignments);
        using var followLocation = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, followLocation.StatusCode);
        await using var db = fixture.CreateDbContext();
        var stored = await db.Events.SingleAsync(x => x.Id == result.Id);
        Assert.Null(stored.TemplateId);
        Assert.Null(stored.OccurrenceDate);
    }

    [Fact]
    public async Task One_time_event_rejects_offsetless_invalid_reversed_and_started_dates()
    {
        using var client = fixture.CreateLeaderClient();
        var valid = EventRequest();
        CreateEventRequest[] requests = [valid with { StartsAt = "2027-01-01T10:00:00" }, valid with { StartsAt = "bad" },
            valid with { EndsAt = valid.StartsAt }, valid with { StartsAt = "2000-01-01T10:00:00Z", EndsAt = "2000-01-01T11:00:00Z" },
            valid with { Title = "" }, valid with { Location = new string('x', 241) }];
        foreach (var request in requests)
        {
            using var response = await client.PostAsJsonAsync($"{Leader}/events", request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Additive_template_seed_is_repeatable_without_resetting_existing_template()
    {
        await using var db = fixture.CreateDbContext();
        var seeded = await db.Set<EventTemplate>().SingleAsync(x => x.Id == RecurrenceSeed.TemplateId);
        var originalDate = seeded.StartsOn;
        await RecurrenceSeed.SeedAsync(db, TimeProvider.System);
        Assert.Equal(1, await db.Set<EventTemplate>().CountAsync(x => x.Id == RecurrenceSeed.TemplateId));
        Assert.Equal(originalDate, (await db.Set<EventTemplate>().AsNoTracking().SingleAsync(x => x.Id == RecurrenceSeed.TemplateId)).StartsOn);
    }

    private static async Task<EventTemplateDto> CreateTemplate(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync($"{Leader}/event-templates", TemplateRequest);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EventTemplateDto>())!;
    }

    private static CreateEventRequest EventRequest()
    {
        var start = DateTimeOffset.UtcNow.AddDays(10).ToOffset(TimeSpan.FromHours(-5));
        return new("One-time gathering", "A shared afternoon", "Main hall", start.ToString("O"), start.AddHours(2).ToString("O"));
    }
}

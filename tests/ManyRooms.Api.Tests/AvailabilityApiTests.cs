using System.Net;
using System.Net.Http.Json;
using ManyRooms.Application;
using ManyRooms.Domain;
using ManyRooms.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ManyRooms.Api.Tests;

public sealed class AvailabilityApiTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static string Path => $"/api/organizations/{DemoData.OrganizationId}/availability";
    private static readonly DateTimeOffset Start = new(DateTime.UtcNow.Date.AddDays(30).AddHours(15), TimeSpan.Zero);

    [Fact]
    public async Task Own_availability_can_be_created_refetched_updated_and_deleted_with_concurrency_checks()
    {
        using var client = fixture.CreateClient();
        using var createdResponse = await client.PostAsJsonAsync(Path, new
        {
            startsAt = Start.ToOffset(TimeSpan.FromHours(-5)).ToString("O"),
            endsAt = Start.AddHours(2).ToOffset(TimeSpan.FromHours(-5)).ToString("O"),
            note = "  Family visit  "
        });
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.NotNull(createdResponse.Headers.Location);
        var created = await createdResponse.Content.ReadFromJsonAsync<UnavailabilityDto>();
        Assert.NotNull(created);
        Assert.Equal("Family visit", created.Note);
        Assert.Equal(1, created.Version);
        Assert.Equal(TimeSpan.Zero, created.StartsAt.Offset);
        using var refreshedClient = fixture.CreateClient();
        var located = await refreshedClient.GetFromJsonAsync<UnavailabilityDto>(createdResponse.Headers.Location);
        Assert.Equal(created.Id, located!.Id);
        var list = await refreshedClient.GetFromJsonAsync<UnavailabilityDto[]>(Path);
        Assert.NotNull(list);
        Assert.Contains(list, x => x.Id == created.Id && x.Note == "Family visit");
        Assert.Equal(list.OrderBy(x => x.StartsAt).Select(x => x.Id), list.Select(x => x.Id));

        using var updatedResponse = await refreshedClient.PutAsJsonAsync($"{Path}/{created.Id}", new
        {
            startsAt = Start.AddDays(1),
            endsAt = Start.AddDays(1).AddHours(1),
            note = "  ",
            expectedVersion = 1
        });
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<UnavailabilityDto>();
        Assert.NotNull(updated);
        Assert.Equal(2, updated.Version);
        Assert.Null(updated.Note);
        await using (var db = fixture.CreateDbContext())
        {
            var stored = await db.Unavailability.SingleAsync(x => x.Id == created.Id);
            var membership = await db.Memberships.SingleAsync(x => x.OrganizationId == DemoData.OrganizationId && x.UserId == DemoData.UserId);
            Assert.Equal(membership.Id, stored.MembershipId);
            Assert.Equal(DemoData.OrganizationId, stored.OrganizationId);
            Assert.Equal(updated.StartsAt, stored.StartsAt);
            Assert.True(stored.UpdatedAt >= stored.CreatedAt);
        }

        using var staleEdit = await client.PutAsJsonAsync($"{Path}/{created.Id}", new
        {
            startsAt = Start,
            endsAt = Start.AddHours(1),
            note = "Stale",
            expectedVersion = 1
        });
        using var staleDelete = await client.DeleteAsync($"{Path}/{created.Id}?expectedVersion=1");
        Assert.Equal(HttpStatusCode.Conflict, staleEdit.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleDelete.StatusCode);
        using var deleted = await client.DeleteAsync($"{Path}/{created.Id}?expectedVersion=2");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await using var afterDelete = fixture.CreateDbContext();
        Assert.False(await afterDelete.Unavailability.AnyAsync(x => x.Id == created.Id));
    }

    public static TheoryData<string?, string?, string?> InvalidRanges => new()
    {
        { "2026-11-10T10:00:00Z", "2026-11-10T10:00:00Z", null },
        { "2026-11-10T10:00:00Z", "2026-11-10T09:00:00Z", null },
        { "2026-11-10T10:00:00", "2026-11-10T11:00:00Z", null },
        { "2026-11-10T10:00:00Z", "2026-11-10T11:00:00", null },
        { "not a date", "2026-11-10T11:00:00Z", null },
        { null, "2026-11-10T11:00:00Z", null },
        { "2026-11-10T10:00:00Z", "2026-11-10T11:00:00Z", new string('x', 501) }
    };

    [Theory]
    [MemberData(nameof(InvalidRanges))]
    public async Task Invalid_range_or_note_returns_bad_request_without_persisting(string? startsAt, string? endsAt, string? note)
    {
        await using var db = fixture.CreateDbContext();
        var before = await db.Unavailability.CountAsync();
        using var client = fixture.CreateClient();
        using var response = await client.PostAsJsonAsync(Path, new { startsAt, endsAt, note });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, await db.Unavailability.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Availability_writes_require_positive_expected_version(int expectedVersion)
    {
        var item = await StoreUnavailabilityAsync(DemoData.OrganizationId, DemoData.UserId);
        using var client = fixture.CreateClient();
        using var update = await client.PutAsJsonAsync($"{Path}/{item.Id}", new
        {
            startsAt = Start,
            endsAt = Start.AddHours(1),
            note = "Invalid version",
            expectedVersion
        });
        using var delete = await client.DeleteAsync($"{Path}/{item.Id}?expectedVersion={expectedVersion}");
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.Equal(1, (await db.Unavailability.SingleAsync(x => x.Id == item.Id)).Version);
    }

    [Fact]
    public async Task Another_volunteers_availability_is_hidden_and_cannot_be_modified()
    {
        var item = await StoreUnavailabilityAsync(DemoData.OrganizationId, DemoData.LeaderUserId);
        using var client = fixture.CreateClient();
        var list = await client.GetFromJsonAsync<UnavailabilityDto[]>(Path);
        Assert.DoesNotContain(list!, x => x.Id == item.Id);
        using var update = await client.PutAsJsonAsync($"{Path}/{item.Id}", new
        {
            startsAt = Start,
            endsAt = Start.AddHours(1),
            note = "Unauthorized",
            expectedVersion = 1
        });
        using var delete = await client.DeleteAsync($"{Path}/{item.Id}?expectedVersion=1");
        using var read = await client.GetAsync($"{Path}/{item.Id}");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.Equal("Private test note", (await db.Unavailability.SingleAsync(x => x.Id == item.Id)).Note);
    }

    [Fact]
    public async Task Organization_boundary_applies_to_lists_creation_and_foreign_record_ids()
    {
        await using var db = fixture.CreateDbContext();
        var foreignMembership = await db.Memberships.SingleAsync(x => x.OrganizationId == DemoData.OtherOrganizationId);
        var foreign = await StoreUnavailabilityAsync(DemoData.OtherOrganizationId, foreignMembership.UserId);
        using var client = fixture.CreateClient();
        var otherPath = $"/api/organizations/{DemoData.OtherOrganizationId}/availability";
        using var list = await client.GetAsync(otherPath);
        using var create = await client.PostAsJsonAsync(otherPath, new { startsAt = Start, endsAt = Start.AddHours(1), note = (string?)null });
        using var foreignId = await client.DeleteAsync($"{Path}/{foreign.Id}?expectedVersion=1");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignId.StatusCode);
    }

    [Fact]
    public async Task Caller_cannot_choose_someone_elses_membership_on_creation()
    {
        await using var db = fixture.CreateDbContext();
        var otherMembership = await db.Memberships.SingleAsync(x => x.OrganizationId == DemoData.OrganizationId && x.UserId == DemoData.LeaderUserId);
        using var client = fixture.CreateClient();
        using var response = await client.PostAsJsonAsync(Path, new
        {
            startsAt = Start,
            endsAt = Start.AddHours(1),
            note = "My own time",
            membershipId = otherMembership.Id,
            organizationId = DemoData.OtherOrganizationId,
            userId = DemoData.LeaderUserId
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<UnavailabilityDto>();
        var stored = await db.Unavailability.SingleAsync(x => x.Id == created!.Id);
        var owner = await db.Memberships.SingleAsync(x => x.Id == stored.MembershipId);
        Assert.Equal(DemoData.UserId, owner.UserId);
        Assert.Equal(DemoData.OrganizationId, stored.OrganizationId);
    }

    [Fact]
    public async Task Database_rejects_cross_organization_availability_membership()
    {
        await using var db = fixture.CreateDbContext();
        var foreignMembership = await db.Memberships.SingleAsync(x => x.OrganizationId == DemoData.OtherOrganizationId);
        db.Unavailability.Add(VolunteerUnavailability.Create(DemoData.OrganizationId, foreignMembership.Id,
            Start, Start.AddHours(1), null, DateTimeOffset.UtcNow));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [Fact]
    public async Task Repeating_seed_does_not_resurrect_a_deleted_demo_unavailable_range()
    {
        await using var db = fixture.CreateDbContext();
        var seeded = await db.Unavailability.OrderBy(x => x.CreatedAt).FirstAsync();
        var id = seeded.Id;
        db.Unavailability.Remove(seeded);
        await db.SaveChangesAsync();
        await DemoData.SeedAsync(db, TimeProvider.System);
        Assert.False(await db.Unavailability.AnyAsync(x => x.Id == id));
    }

    private async Task<VolunteerUnavailability> StoreUnavailabilityAsync(Guid organizationId, Guid userId)
    {
        await using var db = fixture.CreateDbContext();
        var membership = await db.Memberships.SingleAsync(x => x.OrganizationId == organizationId && x.UserId == userId);
        var item = VolunteerUnavailability.Create(organizationId, membership.Id, Start, Start.AddHours(1), "Private test note", DateTimeOffset.UtcNow);
        db.Unavailability.Add(item);
        await db.SaveChangesAsync();
        return item;
    }
}

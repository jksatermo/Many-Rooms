using ManyRooms.Domain;
using ManyRooms.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ManyRooms.Api.Tests;

public sealed class MigrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Forward_migration_preserves_existing_milestone_one_assignment_history_and_supports_availability()
    {
        var schema = $"many_rooms_forward_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(fixture.ConnectionString);
        await admin.OpenAsync();
        await using (var createSchema = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin))
            await createSchema.ExecuteNonQueryAsync();
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { SearchPath = schema }.ConnectionString;
        var options = new DbContextOptionsBuilder<ManyRoomsDbContext>().UseNpgsql(connectionString).Options;
        try
        {
            await using var before = new ManyRoomsDbContext(options);
            await before.GetService<IMigrator>().MigrateAsync("20261010034940_InitialScheduling");
            Assert.Single(await before.Database.GetAppliedMigrationsAsync());
            var now = new DateTimeOffset(DateTime.UtcNow.Date.AddHours(12), TimeSpan.Zero);
            var organization = new Organization { Name = "Existing fictional community", TimeZone = "America/Chicago" };
            var user = new User { DisplayName = "Existing volunteer", Email = "existing@example.invalid" };
            var membership = new OrganizationMembership { OrganizationId = organization.Id, UserId = user.Id, User = user };
            var team = new Team { OrganizationId = organization.Id, Name = "Welcome team" };
            var member = new TeamMember
            {
                OrganizationId = organization.Id,
                TeamId = team.Id,
                Team = team,
                MembershipId = membership.Id,
                Membership = membership
            };
            var item = new Event
            {
                OrganizationId = organization.Id,
                Title = "Existing gathering",
                Description = "Preserved from Milestone 1",
                Location = "Community hall",
                StartsAt = now.AddDays(3),
                EndsAt = now.AddDays(3).AddHours(2)
            };
            var assignment = new VolunteerAssignment
            {
                OrganizationId = organization.Id,
                TeamMemberId = member.Id,
                TeamMember = member,
                EventId = item.Id,
                Event = item,
                RoleName = "Welcome host",
                CreatedAt = now,
                UpdatedAt = now
            };
            assignment.Respond(AssignmentStatus.Accepted, 1, now);
            var history = new AssignmentResponse
            {
                OrganizationId = organization.Id,
                AssignmentId = assignment.Id,
                MembershipId = membership.Id,
                Decision = AssignmentStatus.Accepted,
                AssignmentVersion = 2,
                RespondedAt = now
            };
            before.AddRange(organization, assignment, history);
            await before.SaveChangesAsync();

            await before.Database.MigrateAsync();

            await using var after = new ManyRoomsDbContext(options);
            var stored = await after.Assignments.SingleAsync(x => x.Id == assignment.Id);
            Assert.Equal(AssignmentStatus.Accepted, stored.Status);
            Assert.Equal(2, stored.Version);
            Assert.Equal(now, stored.RespondedAt);
            Assert.Equal(now, stored.CreatedAt);
            Assert.Equal(item.StartsAt, (await after.Events.SingleAsync(x => x.Id == item.Id)).StartsAt);
            var storedHistory = Assert.Single(await after.Responses.ToListAsync());
            Assert.Equal(history.Id, storedHistory.Id);
            Assert.Equal(AssignmentStatus.Accepted, storedHistory.Decision);
            Assert.Equal(membership.Id, storedHistory.MembershipId);
            Assert.Empty(await after.Database.GetPendingMigrationsAsync());
            Assert.False(after.Database.HasPendingModelChanges());
            var unavailable = VolunteerUnavailability.Create(organization.Id, membership.Id,
                item.StartsAt, item.EndsAt, "Added after upgrade", now);
            after.Unavailability.Add(unavailable);
            await after.SaveChangesAsync();
            after.ChangeTracker.Clear();
            Assert.Equal("Added after upgrade", (await after.Unavailability.SingleAsync()).Note);
        }
        finally
        {
            await using var dropSchema = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE", admin);
            await dropSchema.ExecuteNonQueryAsync();
        }
    }
}

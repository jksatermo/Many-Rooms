using ManyRooms.Application;
using ManyRooms.Domain;
using ManyRooms.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ManyRooms.Api.Tests;

/// <summary>Runs migrations and HTTP tests against PostgreSQL in a disposable, isolated schema.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string schema = $"many_rooms_tests_{Guid.NewGuid():N}";
    private string? originalConnection;
    public string ConnectionString { get; private set; } = "";
    public DemoApiFactory Factory { get; private set; } = null!;
    public ConcurrentReadGate ConcurrentReads { get; } = new();

    public async Task InitializeAsync()
    {
        originalConnection = Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION");
        if (string.IsNullOrWhiteSpace(originalConnection))
            throw new InvalidOperationException(
                "PostgreSQL integration tests require TEST_DATABASE_CONNECTION. Start the development database and run npm run test:backend; see README. These tests are never silently skipped.");

        await using var connection = new NpgsqlConnection(originalConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", connection);
        await command.ExecuteNonQueryAsync();
        ConnectionString = new NpgsqlConnectionStringBuilder(originalConnection) { SearchPath = schema }.ConnectionString;
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
        await DemoData.SeedAsync(db, TimeProvider.System);
        Factory = new DemoApiFactory(ConnectionString, ConcurrentReads);
    }

    public ManyRoomsDbContext CreateDbContext() => new(new DbContextOptionsBuilder<ManyRoomsDbContext>()
        .UseNpgsql(ConnectionString).Options);

    public HttpClient CreateClient(bool demo = true)
    {
        var client = Factory.CreateClient();
        if (demo) client.DefaultRequestHeaders.Add("X-ManyRooms-Demo", "true");
        return client;
    }

    public async Task<VolunteerAssignment> CreateAssignmentAsync(bool own = true, bool started = false)
    {
        await using var db = CreateDbContext();
        var member = await db.TeamMembers.Include(x => x.Membership)
            .Where(x => x.OrganizationId == DemoData.OrganizationId &&
                (own ? x.Membership.UserId == DemoData.UserId : x.Membership.UserId != DemoData.UserId))
            .FirstAsync();
        var start = DateTimeOffset.UtcNow.AddDays(started ? -1 : 14);
        var item = new Event
        {
            OrganizationId = DemoData.OrganizationId,
            Title = "Integration test gathering",
            Description = "Isolated test event",
            Location = "Test hall",
            StartsAt = start,
            EndsAt = start.AddHours(2)
        };
        var assignment = new VolunteerAssignment
        {
            OrganizationId = item.OrganizationId,
            Event = item,
            EventId = item.Id,
            TeamMemberId = member.Id,
            RoleName = "Test welcome volunteer"
        };
        db.Add(assignment);
        await db.SaveChangesAsync();
        return assignment;
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        if (originalConnection is null || string.IsNullOrEmpty(ConnectionString)) return;
        await using var connection = new NpgsqlConnection(originalConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE", connection);
        await command.ExecuteNonQueryAsync();
    }
}

public sealed class DemoApiFactory(string connectionString, ConcurrentReadGate? concurrentReads = null,
    string environment = "Development", bool demoEnabled = true) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:ManyRooms", connectionString);
        builder.UseSetting("Demo:Enabled", demoEnabled.ToString());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:ManyRooms"] = connectionString,
                ["Demo:Enabled"] = demoEnabled.ToString()
            }));
        if (concurrentReads is null) return;
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAssignmentStore>();
            services.AddScoped<IAssignmentStore>(provider => new GatedAssignmentStore(
                provider.GetRequiredService<CommunityStore>(), concurrentReads));
        });
    }
}

/// <summary>Forces two genuine HTTP requests to read the same version before either can save.</summary>
public sealed class ConcurrentReadGate
{
    private Guid? assignmentId;
    private int arrivals;
    private TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Arm(Guid id)
    {
        assignmentId = id;
        arrivals = 0;
        ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public async Task WaitAsync(Guid id, CancellationToken cancellationToken)
    {
        if (assignmentId != id) return;
        if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
    }
}

internal sealed class GatedAssignmentStore(CommunityStore store, ConcurrentReadGate gate) : IAssignmentStore
{
    public Task<bool> IsMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        store.IsMemberAsync(organizationId, userId, cancellationToken);

    public async Task<VolunteerAssignment?> FindAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken)
    {
        var result = await store.FindAsync(organizationId, assignmentId, cancellationToken);
        await gate.WaitAsync(assignmentId, cancellationToken);
        return result;
    }

    public Task SaveResponseAsync(VolunteerAssignment assignment, AssignmentResponse response, CancellationToken cancellationToken) =>
        store.SaveResponseAsync(assignment, response, cancellationToken);
}

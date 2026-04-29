using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Npgsql;
using Wolverine;
using Wolverine.Tracking;

namespace Modulith.Modules.Catalog.IntegrationTests.Integration;

/// <summary>
/// Durability integration tests validating the durable Wolverine outbox:
///   - the wolverine schema is provisioned at startup
///   - messages flow through the persistent outbox (not fire-and-forget)
///   - duplicate event delivery does not produce duplicate side effects in Catalog
/// </summary>
[Collection("CrossModule")]
[Trait("Category", "Integration")]
public sealed class DurabilityTests(CrossModuleApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task WolverineSchema_IsProvisionedAtStartup()
    {
        // The wolverine schema and its tables must exist after host startup so the
        // durable outbox can function. This test queries the information_schema to
        // confirm provisioning happened automatically.
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = 'wolverine'
              AND table_name IN (
                  'wolverine_incoming_envelopes',
                  'wolverine_outgoing_envelopes',
                  'wolverine_dead_letters')
            """;

        var count = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task RegisteringUser_LeavesNoDeadLetters()
    {
        // A clean message delivery must not land anything in the dead-letter queue.
        var request = new { Email = "dead-letter-check@example.com", Password = "Password1!", DisplayName = "Clean" };

        Func<IMessageContext, Task> act = async _ =>
        {
            var response = await _client.PostAsJsonAsync("/v1/users/register", request);
            response.EnsureSuccessStatusCode();
        };
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(act);

        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM wolverine.wolverine_dead_letters";
        var dead = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(0, dead);
    }

    [Fact]
    public async Task DuplicateDelivery_UserRegisteredV1_DoesNotCreateDuplicateCatalogCustomer()
    {
        // Arrange — register so the user exists in the DB; capture their ID
        var email = "duplicate-delivery@example.com";
        var request = new { Email = email, Password = "Password1!", DisplayName = "Duplicate" };
        HttpResponseMessage? registerResponse = null;

        Func<IMessageContext, Task> act = async _ =>
        {
            registerResponse = await _client.PostAsJsonAsync("/v1/users/register", request);
        };
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(act);

        registerResponse!.EnsureSuccessStatusCode();
        var body = await registerResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var userId = body!.RootElement.GetProperty("userId").GetGuid();

        // Act — re-deliver the same UserRegisteredV1 (simulates at-least-once redelivery)
        var redelivered = new UserRegisteredV1(userId, email, "Duplicate", Guid.NewGuid());
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .InvokeMessageAndWaitAsync(redelivered);

        // Assert — idempotency: still exactly one Customer row for this user
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var customerCount = await db.Customers.CountAsync(c => c.UserId == userId);
        Assert.Equal(1, customerCount);
    }
}

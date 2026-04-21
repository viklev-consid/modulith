using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;

namespace Modulith.Modules.Audit.IntegrationTests.Integration;

[Collection("AuditCrossModule")]
[Trait("Category", "Integration")]
public sealed class OnUserRegisteredAuditTests(AuditCrossModuleFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RegisteringUser_CreatesUserRegisteredAuditEntry()
    {
        // Arrange
        var request = new { Email = "audit-test@example.com", Password = "Password1!", DisplayName = "Audit Test" };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/users/register", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var userId = body!.RootElement.GetProperty("userId").GetGuid();

        // Assert — poll until Wolverine outbox delivers UserRegisteredV1 and the audit entry is written
        Domain.AuditEntry? entry = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!cts.IsCancellationRequested)
        {
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            entry = await db.AuditEntries
                .FirstOrDefaultAsync(e => e.ActorId == userId && e.EventType == "user.registered", cts.Token);
            if (entry is not null)
            {
                break;
            }

            await Task.Delay(200, cts.Token);
        }

        Assert.NotNull(entry);
        Assert.Equal("user.registered", entry.EventType);
        Assert.Equal(userId, entry.ActorId);
        Assert.Equal("User", entry.ResourceType);
        Assert.Equal(userId, entry.ResourceId);
    }

    [Fact]
    public async Task GetAuditTrail_WithAuthentication_ReturnsUserEntries()
    {
        // Arrange — register a user so there is an audit entry
        var registerRequest = new { Email = "audit-trail@example.com", Password = "Password1!", DisplayName = "Trail User" };
        var registerResponse = await _client.PostAsJsonAsync("/v1/users/register", registerRequest);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var body = await registerResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var userId = body!.RootElement.GetProperty("userId").GetGuid();

        // Wait for the audit entry to be created
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        bool entryFound = false;
        while (!cts.IsCancellationRequested && !entryFound)
        {
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            entryFound = await db.AuditEntries.AnyAsync(
                e => e.ActorId == userId, cts.Token);
            if (!entryFound)
            {
                await Task.Delay(200, cts.Token);
            }
        }
        Assert.True(entryFound, "Audit entry was not created within the timeout.");

        // Act — request the audit trail as the registered user
        using var authClient = fixture.CreateAuthenticatedClient(userId, "audit-trail@example.com", "Trail User");
        var trailResponse = await authClient.GetAsync("/v1/audit/trail");

        // Assert
        Assert.Equal(HttpStatusCode.OK, trailResponse.StatusCode);
        var trail = await trailResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(trail);
        Assert.True(trail.RootElement.GetProperty("total").GetInt32() >= 1);
    }
}

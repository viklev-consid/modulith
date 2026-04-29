using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;
using Wolverine;
using Wolverine.Tracking;

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
        HttpResponseMessage? registerResponse = null;

        // Act — TrackActivity waits for all cascading messages to finish before returning
        Func<IMessageContext, Task> act = async _ =>
        {
            registerResponse = await _client.PostAsJsonAsync("/v1/users/register", request);
        };
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(act);

        Assert.Equal(HttpStatusCode.Created, registerResponse!.StatusCode);
        var body = await registerResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var userId = body!.RootElement.GetProperty("userId").GetGuid();

        // Assert — no polling needed; TrackActivity waited for all subscribers to complete
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var entry = await db.AuditEntries
            .FirstOrDefaultAsync(e => e.ActorId == userId && e.EventType == "user.registered");

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
        HttpResponseMessage? registerResponse = null;

        Func<IMessageContext, Task> act = async _ =>
        {
            registerResponse = await _client.PostAsJsonAsync("/v1/users/register", registerRequest);
        };
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(act);

        Assert.Equal(HttpStatusCode.Created, registerResponse!.StatusCode);
        var body = await registerResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var userId = body!.RootElement.GetProperty("userId").GetGuid();

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

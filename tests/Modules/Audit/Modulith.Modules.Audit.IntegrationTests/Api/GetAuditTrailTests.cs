using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Contracts.Queries;
using Modulith.Modules.Audit.Persistence;

namespace Modulith.Modules.Audit.IntegrationTests.Api;

[Collection("AuditCrossModule")]
[Trait("Category", "Integration")]
public sealed class GetAuditTrailTests(AuditCrossModuleFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _anon = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Registers a user and waits until the Wolverine outbox has delivered the
    // UserRegisteredV1 event and the audit entry has been written to the DB.
    private async Task<Guid> RegisterAndWaitForAuditEntryAsync(string email, string name = "Test User")
    {
        var resp = await _anon.PostAsJsonAsync("/v1/users/register",
            new { Email = email, Password = "Password1!", DisplayName = name });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        var userId = body!.RootElement.GetProperty("userId").GetGuid();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!cts.IsCancellationRequested)
        {
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            if (await db.AuditEntries.AnyAsync(e => e.ActorId == userId, cts.Token))
                break;
            await Task.Delay(200, cts.Token);
        }

        return userId;
    }

    // ── Ownership enforcement ──────────────────────────────────────────────

    [Fact]
    public async Task GetAuditTrail_UserRequestsOtherUsersTrail_Returns403()
    {
        // Arrange
        var ownerId = await RegisterAndWaitForAuditEntryAsync("owner@example.com", "Owner");
        var otherId = await RegisterAndWaitForAuditEntryAsync("other@example.com", "Other");

        var otherClient = fixture.CreateAuthenticatedClient(otherId, "other@example.com", "Other", "user");

        // Act — regular user passes a different user's actorId
        var response = await otherClient.GetAsync($"/v1/audit/trail?actorId={ownerId}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAuditTrail_UserPassesExplicitOwnActorId_Returns200WithOwnEntries()
    {
        // Arrange
        var userId = await RegisterAndWaitForAuditEntryAsync("owner@example.com", "Owner");
        var client = fixture.CreateAuthenticatedClient(userId, "owner@example.com", "Owner", "user");

        // Act — explicitly passing own actorId is equivalent to the implicit default
        var response = await client.GetAsync($"/v1/audit/trail?actorId={userId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetAuditTrailResponse>();
        Assert.NotNull(body);
        Assert.True(body.Total >= 1);
        Assert.All(body.Entries, e => Assert.Equal(userId, e.ActorId));
    }

    // ── Admin override ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAuditTrail_AdminRequestsOtherUsersTrail_Returns200WithTargetEntries()
    {
        // Arrange
        var targetId = await RegisterAndWaitForAuditEntryAsync("target@example.com", "Target");
        var adminId = await RegisterAndWaitForAuditEntryAsync("admin@example.com", "Admin");

        // An admin JWT carries the audit.trail.read permission via PermissionClaimsTransformation.
        var adminClient = fixture.CreateAuthenticatedClient(adminId, "admin@example.com", "Admin", "admin");

        // Act — admin passes a different user's actorId
        var response = await adminClient.GetAsync($"/v1/audit/trail?actorId={targetId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetAuditTrailResponse>();
        Assert.NotNull(body);
        Assert.True(body.Total >= 1);
        Assert.All(body.Entries, e => Assert.Equal(targetId, e.ActorId));
    }
}

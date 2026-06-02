using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.RefreshToken;
using Modulith.Modules.Users.Features.Register;
using Wolverine;
using Wolverine.Tracking;

namespace Modulith.Modules.Audit.IntegrationTests.Integration;

[Collection("AuditCrossModule")]
[Trait("Category", "Integration")]
public sealed class OnRefreshTokenReuseDetectedAuditTests(AuditCrossModuleFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReuseDetected_CreatesAuditEntry()
    {
        // Arrange — register and login to get a valid session
        var (userId, _, refreshToken) = await RegisterAndLoginAsync("reuse-audit@example.com");

        // First use — legitimate rotation
        await client.PostAsJsonAsync("/v1/users/token/refresh",
            new RefreshTokenRequest(refreshToken));

        // Act — reuse the already-rotated token under TrackActivity so the audit subscriber completes
        Func<IMessageContext, Task> act = async _ =>
        {
            await client.PostAsJsonAsync("/v1/users/token/refresh",
                new RefreshTokenRequest(refreshToken));
        };
        await fixture.ApplicationHost
            .TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(act);

        // Assert — audit entry created for the reuse event
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var entry = await db.AuditEntries
            .FirstOrDefaultAsync(e =>
                e.ActorId == userId &&
                e.EventType == "user.refresh_token_reuse_detected");

        Assert.NotNull(entry);
        Assert.Equal("User", entry.ResourceType);
        Assert.Equal(userId, entry.ResourceId);
    }

    [Fact]
    public async Task NormalRefresh_DoesNotCreateReuseAuditEntry()
    {
        // Arrange — register and login
        var (userId, _, refreshToken) = await RegisterAndLoginAsync("reuse-no-audit@example.com");

        // Act — normal refresh under TrackActivity
        Func<IMessageContext, Task> act = async _ =>
        {
            var resp = await client.PostAsJsonAsync("/v1/users/token/refresh",
                new RefreshTokenRequest(refreshToken));
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        };
        await fixture.ApplicationHost
            .TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(act);

        // Assert — no reuse audit entry exists for this user
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var count = await db.AuditEntries
            .CountAsync(e =>
                e.ActorId == userId &&
                e.EventType == "user.refresh_token_reuse_detected");

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task DuplicateDelivery_CreatesSingleAuditEntry()
    {
        // Arrange — fabricate an event with a fixed EventId to simulate at-least-once re-delivery
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var @event = new RefreshTokenReuseDetectedV1(userId, eventId);

        // Act — deliver the same event twice
        await fixture.ApplicationHost
            .TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .InvokeMessageAndWaitAsync(@event);

        await fixture.ApplicationHost
            .TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .InvokeMessageAndWaitAsync(@event);

        // Assert — idempotency key ensures exactly one row despite two deliveries
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var count = await db.AuditEntries
            .CountAsync(e => e.IdempotencyKey == eventId);

        Assert.Equal(1, count);
    }

    private async Task<(Guid UserId, string AccessToken, string RefreshToken)> RegisterAndLoginAsync(string email)
    {
        HttpResponseMessage? registerResponse = null;
        Func<IMessageContext, Task> act = async _ =>
        {
            registerResponse = await client.PostAsJsonAsync(
                "/v1/users/register",
                new RegisterRequest(email, "Password1!", "Test User"));
        };
        await fixture.ApplicationHost
            .TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(act);

        Assert.Equal(HttpStatusCode.Created, registerResponse!.StatusCode);
        var body = await registerResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var userId = body!.RootElement.GetProperty("userId").GetGuid();
        await fixture.ConfirmEmailAsync(email);

        var loginResp = await client.PostAsJsonAsync("/v1/users/login",
            new LoginRequest(email, "Password1!"));
        var login = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);

        return (userId, login.AccessToken, login.RefreshToken);
    }
}

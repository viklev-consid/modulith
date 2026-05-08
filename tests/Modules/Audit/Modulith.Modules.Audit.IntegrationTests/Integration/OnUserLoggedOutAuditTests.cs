using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.Logout;
using Modulith.Modules.Users.Features.Register;
using Wolverine;
using Wolverine.Tracking;

namespace Modulith.Modules.Audit.IntegrationTests.Integration;

[Collection("AuditCrossModule")]
[Trait("Category", "Integration")]
public sealed class OnUserLoggedOutAuditTests(AuditCrossModuleFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Logout_WithActiveToken_CreatesAuditEntry()
    {
        // Arrange — register and login to get a valid session
        var (userId, accessToken, refreshToken) = await RegisterAndLoginAsync("logout-active@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        // Act — logout within TrackActivity so we wait for the audit subscriber to complete
        Func<IMessageContext, Task> act = async _ =>
        {
            await auth.PostAsJsonAsync("/v1/users/logout", new LogoutRequest(refreshToken));
        };
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(act);

        // Assert
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var entry = await db.AuditEntries
            .FirstOrDefaultAsync(e => e.ActorId == userId && e.EventType == "user.logged_out");

        Assert.NotNull(entry);
        Assert.Equal("User", entry.ResourceType);
        Assert.Equal(userId, entry.ResourceId);
    }

    [Fact]
    public async Task Logout_WithUnknownToken_DoesNotCreateAuditEntry()
    {
        // Arrange — register so we have a valid access token for the authenticated endpoint
        var (userId, accessToken, _) = await RegisterAndLoginAsync("logout-unknown@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        // Act — logout with a token that does not exist in the database
        Func<IMessageContext, Task> act = async _ =>
        {
            await auth.PostAsJsonAsync("/v1/users/logout", new LogoutRequest("this-token-does-not-exist"));
        };
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(act);

        // Assert — endpoint returns success but no audit entry is created
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var entry = await db.AuditEntries
            .FirstOrDefaultAsync(e => e.ActorId == userId && e.EventType == "user.logged_out");

        Assert.Null(entry);
    }

    [Fact]
    public async Task Logout_WithAlreadyRevokedToken_DoesNotCreateSecondAuditEntry()
    {
        // Arrange — register, login, and perform first logout (which revokes the token)
        var (userId, accessToken, refreshToken) = await RegisterAndLoginAsync("logout-revoked@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        Func<IMessageContext, Task> firstLogout = async _ =>
        {
            await auth.PostAsJsonAsync("/v1/users/logout", new LogoutRequest(refreshToken));
        };
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(firstLogout);

        // Act — second logout with the same (now-revoked) token
        Func<IMessageContext, Task> secondLogout = async _ =>
        {
            await auth.PostAsJsonAsync("/v1/users/logout", new LogoutRequest(refreshToken));
        };
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(secondLogout);

        // Assert — exactly one audit entry from the first logout; the second is a no-op
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var count = await db.AuditEntries
            .CountAsync(e => e.ActorId == userId && e.EventType == "user.logged_out");

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Logout_DuplicateEventDelivery_CreatesSingleAuditEntry()
    {
        // Arrange — fabricate an event with a fixed EventId to simulate at-least-once re-delivery
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var @event = new UserLoggedOutV1(userId, eventId);

        // Act — deliver the same event twice (simulates Wolverine re-delivery after transient failure)
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .InvokeMessageAndWaitAsync(@event);

        await fixture.ApplicationHost.TrackActivity()
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
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(act);

        Assert.Equal(HttpStatusCode.Created, registerResponse!.StatusCode);
        var body = await registerResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var userId = body!.RootElement.GetProperty("userId").GetGuid();

        var loginResp = await client.PostAsJsonAsync("/v1/users/login", new LoginRequest(email, "Password1!"));
        var login = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);

        return (userId, login.AccessToken, login.RefreshToken);
    }
}

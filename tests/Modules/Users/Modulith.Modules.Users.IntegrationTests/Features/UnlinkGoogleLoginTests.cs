using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Modulith.TestSupport;
using Wolverine.Tracking;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("GoogleUsersModule")]
[Trait("Category", "Integration")]
public sealed class UnlinkGoogleLoginTests(GoogleUsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _anon = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UnlinkGoogleLogin_WhenLinkedAndHasPassword_Returns204()
    {
        const string subject = "sub-unlink-ok";
        var (userId, accessToken) = await RegisterAndLoginAsync("unlinkgoogle@example.com");
        await SeedExternalLoginAsync(userId, subject);
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        var response = await auth.DeleteAsync("/v1/users/me/auth/google/unlink");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UnlinkGoogleLogin_RemovesExternalLoginFromDatabase()
    {
        const string subject = "sub-unlink-db";
        var (userId, accessToken) = await RegisterAndLoginAsync("unlinkdb@example.com");
        await SeedExternalLoginAsync(userId, subject);
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        await auth.DeleteAsync("/v1/users/me/auth/google/unlink");

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var login = await db.ExternalLogins.FirstOrDefaultAsync(e => e.Subject == subject);
        Assert.Null(login);
    }

    [Fact]
    public async Task UnlinkGoogleLogin_WhenOnlyCredentialIsGoogle_Returns409()
    {
        const string email = "onlygoogle@example.com";
        const string subject = "sub-only-cred";

        // Create external-only user (no password) with Google linked
        Guid userId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var scopeClock = scope.ServiceProvider.GetRequiredService<IClock>();
            var emailVal = Email.Create(email).Value;
            var user = User.CreateExternal(emailVal, "OnlyGoogle", ExternalLoginProvider.Google, subject, scopeClock).Value;
            user.LinkExternalLogin(ExternalLoginProvider.Google, subject, scopeClock.UtcNow);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id.Value;
        }

        var accessToken = ApiTestFixture.GenerateTestToken(userId, email, "OnlyGoogle");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        var response = await auth.DeleteAsync("/v1/users/me/auth/google/unlink");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UnlinkGoogleLogin_WhenGoogleNotLinked_Returns404()
    {
        var (_, accessToken) = await RegisterAndLoginAsync("notlinked@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        var response = await auth.DeleteAsync("/v1/users/me/auth/google/unlink");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnlinkGoogleLogin_RevokesAllActiveRefreshTokens()
    {
        const string subject = "sub-unlink-tokens";
        var (userId, accessToken) = await RegisterAndLoginAsync("unlinkrevoke@example.com");
        await SeedExternalLoginAsync(userId, subject);

        // Seed a second refresh token (simulates a second active session).
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var (token, _) = RefreshToken.Issue(new UserId(userId), TimeSpan.FromDays(30), clock, "ua2", "1.2.3.4");
            db.RefreshTokens.Add(token);
            await db.SaveChangesAsync();
        }

        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);
        await auth.DeleteAsync("/v1/users/me/auth/google/unlink");

        using var verifyScope = fixture.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var active = await verifyDb.RefreshTokens
            .Where(t => t.UserId == new UserId(userId) && t.RevokedAt == null)
            .CountAsync();

        Assert.Equal(0, active);
    }

    [Fact]
    public async Task UnlinkGoogleLogin_WhenUnauthenticated_Returns401()
    {
        var response = await _anon.DeleteAsync("/v1/users/me/auth/google/unlink");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnlinkGoogleLogin_PublishesExternalLoginUnlinkedEvent()
    {
        // Verifies outbox atomicity: ExternalLoginUnlinkedV1 must be enqueued within the same
        // transaction as the domain mutation. If the publish happened after commit the event
        // would be lost on any failure between those two calls — leaving the user without an
        // unlink alert and without an audit entry.
        const string subject = "sub-unlink-event";
        var (userId, accessToken) = await RegisterAndLoginAsync("unlinkpublish@example.com");
        await SeedExternalLoginAsync(userId, subject);
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        await fixture.ApplicationHost
            .TrackActivity()
            .Timeout(TimeSpan.FromSeconds(15))
            .WaitForMessageToBeReceivedAt<ExternalLoginUnlinkedV1>(fixture.ApplicationHost)
            .ExecuteAndWaitAsync((Func<Wolverine.IMessageContext, Task>)(async _ =>
            {
                await auth.DeleteAsync("/v1/users/me/auth/google/unlink");
            }));

        using var scope = fixture.Services.CreateScope();
        var auditDb = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var entry = await auditDb.AuditEntries.FirstOrDefaultAsync(e =>
            e.EventType == "user.external_login.unlinked" && e.ActorId == userId);

        Assert.NotNull(entry);
    }

    private async Task<(Guid UserId, string AccessToken)> RegisterAndLoginAsync(string email)
    {
        var reg = await _anon.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest(email, "Password1!", "Alice"));
        var regBody = await reg.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(regBody);

        var login = await _anon.PostAsJsonAsync("/v1/users/login",
            new LoginRequest(email, "Password1!"));
        var loginBody = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginBody);

        return (regBody.UserId, loginBody.AccessToken);
    }

    private async Task SeedExternalLoginAsync(Guid userId, string subject)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var user = await db.Users
            .Include(u => u.ExternalLogins)
            .FirstAsync(u => u.Id == new UserId(userId));
        user.LinkExternalLogin(ExternalLoginProvider.Google, subject, clock.UtcNow);
        await db.SaveChangesAsync();
    }
}

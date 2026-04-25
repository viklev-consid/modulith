using System.Net;
using System.Net.Http.Json;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.ExternalLogin.Google.Link;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("GoogleUsersModule")]
[Trait("Category", "Integration")]
public sealed class LinkGoogleLoginTests(GoogleUsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _anon = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task LinkGoogleLogin_WithValidToken_Returns204()
    {
        var (_, accessToken) = await RegisterAndLoginAsync("linkgoogle@example.com");
        fixture.GoogleVerifier.SetIdentity("sub-link-1", "ext@google.com", "Alice");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        var response = await auth.PostAsJsonAsync("/v1/users/me/auth/google/link",
            new LinkGoogleLoginRequest("any-id-token"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task LinkGoogleLogin_LinksExternalLoginInDatabase()
    {
        const string subject = "sub-link-db";
        var (userId, accessToken) = await RegisterAndLoginAsync("linkdb@example.com");
        fixture.GoogleVerifier.SetIdentity(subject, "ext2@google.com", "Alice");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        await auth.PostAsJsonAsync("/v1/users/me/auth/google/link",
            new LinkGoogleLoginRequest("any-id-token"));

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var login = await db.ExternalLogins
            .FirstOrDefaultAsync(e => e.Subject == subject);
        Assert.NotNull(login);
        Assert.Equal(new UserId(userId), login.UserId);
    }

    [Fact]
    public async Task LinkGoogleLogin_WhenSubjectAlreadyLinkedToOtherUser_Returns409()
    {
        const string subject = "sub-conflict";

        // First user links this Google subject
        var (_, token1) = await RegisterAndLoginAsync("user1@example.com");
        fixture.GoogleVerifier.SetIdentity(subject, "ext@google.com", "User1");
        var auth1 = fixture.CreateAuthenticatedClientWithToken(token1);
        await auth1.PostAsJsonAsync("/v1/users/me/auth/google/link", new LinkGoogleLoginRequest("tok"));

        // Second user tries to link the same subject
        var (_, token2) = await RegisterAndLoginAsync("user2@example.com");
        fixture.GoogleVerifier.SetIdentity(subject, "ext@google.com", "User2");
        var auth2 = fixture.CreateAuthenticatedClientWithToken(token2);

        var response = await auth2.PostAsJsonAsync("/v1/users/me/auth/google/link",
            new LinkGoogleLoginRequest("tok"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task LinkGoogleLogin_WhenSameSubjectAlreadyLinkedToSelf_Returns409()
    {
        const string subject = "sub-self-link";
        var (_, accessToken) = await RegisterAndLoginAsync("selflink@example.com");
        fixture.GoogleVerifier.SetIdentity(subject, "ext@google.com", "Alice");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        await auth.PostAsJsonAsync("/v1/users/me/auth/google/link", new LinkGoogleLoginRequest("tok"));

        // Link again — same subject → conflict (already linked to OTHER user check fires first)
        var second = await auth.PostAsJsonAsync("/v1/users/me/auth/google/link", new LinkGoogleLoginRequest("tok"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task LinkGoogleLogin_WhenTokenVerificationFails_Returns401()
    {
        var (_, accessToken) = await RegisterAndLoginAsync("linkerr@example.com");
        fixture.GoogleVerifier.SetError(Error.Unauthorized("Users.ExternalLogin.InvalidIdToken", "invalid"));
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        var response = await auth.PostAsJsonAsync("/v1/users/me/auth/google/link",
            new LinkGoogleLoginRequest("bad-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LinkGoogleLogin_WhenUnauthenticated_Returns401()
    {
        var response = await _anon.PostAsJsonAsync("/v1/users/me/auth/google/link",
            new LinkGoogleLoginRequest("any-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
}

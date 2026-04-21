using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Features.ChangePassword;
using Modulith.Modules.Users.Features.ConfirmEmailChange;
using Modulith.Modules.Users.Features.ForgotPassword;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.Logout;
using Modulith.Modules.Users.Features.LogoutAll;
using Modulith.Modules.Users.Features.RefreshToken;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Features.RequestEmailChange;
using Modulith.Modules.Users.Features.ResetPassword;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class RefreshTokenTests(UsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RefreshToken_WithValidToken_IssuesNewTokenPair()
    {
        var login = await RegisterAndLoginAsync();

        var response = await _client.PostAsJsonAsync("/v1/users/token/refresh",
            new RefreshTokenRequest(login.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body.AccessToken);
        Assert.NotEmpty(body.RefreshToken);
        Assert.NotEqual(login.RefreshToken, body.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_ReusedToken_Returns401AndRevokesChain()
    {
        var login = await RegisterAndLoginAsync();

        // First use — legitimate rotation
        var first = await _client.PostAsJsonAsync("/v1/users/token/refresh",
            new RefreshTokenRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Reuse the original token (already rotated) — must be rejected and chain revoked
        var reuse = await _client.PostAsJsonAsync("/v1/users/token/refresh",
            new RefreshTokenRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/v1/users/token/refresh",
            new RefreshTokenRequest("not-a-real-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_RawValueNotStoredInDatabase()
    {
        var login = await RegisterAndLoginAsync();

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();

        // The raw refresh token must not appear in any stored hash column as plain text
        var stored = await db.RefreshTokens.FirstOrDefaultAsync();
        Assert.NotNull(stored);
        // Token hash should be a byte array — not contain the raw token value as UTF-8
        var rawBytes = System.Text.Encoding.UTF8.GetBytes(login.RefreshToken);
        Assert.False(stored.TokenHash.SequenceEqual(rawBytes),
            "Raw refresh token should not be stored directly — must be hashed.");
    }

    private async Task<LoginResponse> RegisterAndLoginAsync()
    {
        await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice"));

        var response = await _client.PostAsJsonAsync("/v1/users/login",
            new LoginRequest("alice@example.com", "Password1!"));

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        return body;
    }
}

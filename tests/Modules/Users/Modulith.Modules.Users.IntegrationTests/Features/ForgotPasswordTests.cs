using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Features.ForgotPassword;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.RefreshToken;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Features.ResetPassword;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class ForgotPasswordTests(UsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ForgotPassword_WithKnownEmail_Returns200()
    {
        await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice"));

        var response = await _client.PostAsJsonAsync("/v1/users/password/forgot",
            new ForgotPasswordRequest("alice@example.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_WithUnknownEmail_AlsoReturns200_AntiEnumeration()
    {
        // Must return identical 200 for unknown email — no user enumeration.
        var response = await _client.PostAsJsonAsync("/v1/users/password/forgot",
            new ForgotPasswordRequest("nobody@example.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_CreatesHashedTokenInDatabase()
    {
        await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice"));

        await _client.PostAsJsonAsync("/v1/users/password/forgot",
            new ForgotPasswordRequest("alice@example.com"));

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var token = await db.SingleUseTokens.FirstOrDefaultAsync();
        Assert.NotNull(token);
        Assert.True(token.TokenHash.Length > 0, "Token hash must be stored.");
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_ResetsPasswordAndRevokesAllRefreshTokens()
    {
        var reg = await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice"));
        var regBody = await reg.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(regBody);

        // Issue a reset token directly via the service — captures the raw value
        string rawToken;
        using (var scope = fixture.Services.CreateScope())
        {
            var tokenService = scope.ServiceProvider.GetRequiredService<ISingleUseTokenService>();
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == Domain.Email.Create("alice@example.com").Value);
            var (_, raw) = tokenService.Create(
                user.Id, Domain.TokenPurpose.PasswordReset, TimeSpan.FromMinutes(30));
            await db.SaveChangesAsync();
            rawToken = raw;
        }

        var response = await _client.PostAsJsonAsync("/v1/users/password/reset",
            new ResetPasswordRequest(rawToken, "NewPassword1!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Pre-existing refresh token must be revoked
        var refreshAttempt = await _client.PostAsJsonAsync("/v1/users/token/refresh",
            new RefreshTokenRequest(regBody.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAttempt.StatusCode);

        // New password must work
        var newLogin = await _client.PostAsJsonAsync("/v1/users/login",
            new LoginRequest("alice@example.com", "NewPassword1!"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/v1/users/password/reset",
            new ResetPasswordRequest("invalid-token", "NewPassword1!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_TokenCannotBeUsedTwice()
    {
        await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice"));

        string rawToken;
        using (var scope = fixture.Services.CreateScope())
        {
            var tokenService = scope.ServiceProvider.GetRequiredService<ISingleUseTokenService>();
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == Domain.Email.Create("alice@example.com").Value);
            var (_, raw) = tokenService.Create(
                user.Id, Domain.TokenPurpose.PasswordReset, TimeSpan.FromMinutes(30));
            await db.SaveChangesAsync();
            rawToken = raw;
        }

        await _client.PostAsJsonAsync("/v1/users/password/reset",
            new ResetPasswordRequest(rawToken, "NewPassword1!"));

        var second = await _client.PostAsJsonAsync("/v1/users/password/reset",
            new ResetPasswordRequest(rawToken, "AnotherPassword1!"));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }
}

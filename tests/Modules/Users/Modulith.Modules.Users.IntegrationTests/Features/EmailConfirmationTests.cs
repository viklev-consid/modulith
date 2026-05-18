using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.ConfirmEmail;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Features.ResendEmailConfirmation;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class EmailConfirmationTests(UsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ConfirmEmail_WithValidToken_ConfirmsAccountAndAllowsLogin()
    {
        var userId = await RegisterAsync("alice@example.com");
        var rawToken = await CreateConfirmationTokenAsync("alice@example.com");

        var beforeLogin = await client.PostAsJsonAsync("/v1/users/login",
            new LoginRequest("alice@example.com", "Password1!"));
        Assert.Equal(HttpStatusCode.Unauthorized, beforeLogin.StatusCode);

        var response = await client.PostAsJsonAsync("/v1/users/email/confirm",
            new ConfirmEmailRequest(rawToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var state = await fixture.QueryDbAsync<UsersDbContext, (bool Confirmed, bool TokenConsumed)>((db, ct) =>
            db.Users
                .Where(u => u.Id == new UserId(userId))
                .Select(u => new ValueTuple<bool, bool>(
                    u.IsEmailConfirmed,
                    db.SingleUseTokens.Any(t => t.UserId == u.Id
                        && t.Purpose == TokenPurpose.EmailConfirmation
                        && t.ConsumedAt != null)))
                .SingleAsync(ct));
        Assert.True(state.Confirmed);
        Assert.True(state.TokenConsumed);

        var afterLogin = await client.PostAsJsonAsync("/v1/users/login",
            new LoginRequest("alice@example.com", "Password1!"));
        Assert.Equal(HttpStatusCode.OK, afterLogin.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmail_WithInvalidToken_Returns400()
    {
        var response = await client.PostAsJsonAsync("/v1/users/email/confirm",
            new ConfirmEmailRequest("invalid-token"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResendEmailConfirmation_ForUnconfirmedUser_ReplacesPendingToken()
    {
        await RegisterAsync("alice@example.com");

        var response = await client.PostAsJsonAsync("/v1/users/email/confirmation/resend",
            new ResendEmailConfirmationRequest("alice@example.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var activeTokenCount = await fixture.QueryDbAsync<UsersDbContext, int>((db, ct) =>
            db.SingleUseTokens.CountAsync(t =>
                t.Purpose == TokenPurpose.EmailConfirmation &&
                t.ConsumedAt == null,
                ct));
        Assert.Equal(1, activeTokenCount);
    }

    [Fact]
    public async Task ResendEmailConfirmation_ForUnknownEmail_ReturnsSameShape()
    {
        var response = await client.PostAsJsonAsync("/v1/users/email/confirmation/resend",
            new ResendEmailConfirmationRequest("nobody@example.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ResendEmailConfirmationResponse>();
        Assert.NotNull(body);
        Assert.Contains("If an account exists", body.Message, StringComparison.Ordinal);
    }

    private async Task<Guid> RegisterAsync(string email)
    {
        var response = await client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest(email, "Password1!", "Alice"));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(body);
        return body.UserId;
    }

    private async Task<string> CreateConfirmationTokenAsync(string email)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ISingleUseTokenService>();
        var user = await db.Users.FirstAsync(u => u.Email == Email.Create(email).Value);

        await db.SingleUseTokens
            .Where(t => t.UserId == user.Id && t.Purpose == TokenPurpose.EmailConfirmation)
            .ExecuteDeleteAsync();

        var (_, rawToken) = tokenService.Create(user.Id, TokenPurpose.EmailConfirmation, TimeSpan.FromHours(24));
        await db.SaveChangesAsync();
        return rawToken;
    }
}

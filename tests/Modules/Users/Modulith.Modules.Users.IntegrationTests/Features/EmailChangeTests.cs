using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Features.ConfirmEmailChange;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.RefreshToken;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Features.RequestEmailChange;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class EmailChangeTests(UsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _anon = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RequestEmailChange_WithAvailableEmail_Returns200()
    {
        var login = await RegisterAndLoginAsync("alice@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(login.AccessToken);

        var response = await auth.PostAsJsonAsync("/v1/users/me/email/request",
            new RequestEmailChangeRequest("newalice@example.com", "Password1!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RequestEmailChange_WithTakenEmail_AlsoReturns200_AntiEnumeration()
    {
        // Register a second user with the target email
        await _anon.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("bob@example.com", "Password1!", "Bob"));

        var login = await RegisterAndLoginAsync("alice@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(login.AccessToken);

        // Requesting to change to an email that's already taken must return 200 (anti-enumeration)
        var response = await auth.PostAsJsonAsync("/v1/users/me/email/request",
            new RequestEmailChangeRequest("bob@example.com", "Password1!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmailChange_WithValidToken_ChangesEmailAndRevokesAllTokens()
    {
        var login = await RegisterAndLoginAsync("alice@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(login.AccessToken);

        // Issue token + pending change directly via service layer
        string rawToken;
        using (var scope = fixture.Services.CreateScope())
        {
            var tokenService = scope.ServiceProvider.GetRequiredService<ISingleUseTokenService>();
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == Domain.Email.Create("alice@example.com").Value);

            var newEmailResult = Domain.Email.Create("newalice@example.com");
            Assert.False(newEmailResult.IsError);

            var (sut, raw) = tokenService.Create(
                user.Id, Domain.TokenPurpose.EmailChange, TimeSpan.FromMinutes(30));

            var pending = Domain.PendingEmailChange.Create(user.Id, newEmailResult.Value, sut.Id);
            db.PendingEmailChanges.Add(pending);
            await db.SaveChangesAsync();
            rawToken = raw;
        }

        var response = await auth.PostAsJsonAsync("/v1/users/me/email/confirm",
            new ConfirmEmailChangeRequest(rawToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // All refresh tokens must be revoked after email change
        var refresh = await _anon.PostAsJsonAsync("/v1/users/token/refresh",
            new RefreshTokenRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        // New email must work for login
        var newLogin = await _anon.PostAsJsonAsync("/v1/users/login",
            new LoginRequest("newalice@example.com", "Password1!"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task ConfirmEmailChange_WithInvalidToken_Returns400()
    {
        var login = await RegisterAndLoginAsync("alice@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(login.AccessToken);

        var response = await auth.PostAsJsonAsync("/v1/users/me/email/confirm",
            new ConfirmEmailChangeRequest("invalid-token"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<LoginResponse> RegisterAndLoginAsync(string email)
    {
        await _anon.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest(email, "Password1!", "Alice"));

        var response = await _anon.PostAsJsonAsync("/v1/users/login",
            new LoginRequest(email, "Password1!"));

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        return body;
    }
}

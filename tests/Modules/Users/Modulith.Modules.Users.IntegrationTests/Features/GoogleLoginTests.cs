using System.Net;
using System.Net.Http.Json;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.ExternalLogin.Google.Login;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("GoogleUsersModule")]
[Trait("Category", "Integration")]
public sealed class GoogleLoginTests(GoogleUsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GoogleLogin_WhenNoAccountLinked_NewUser_Returns202()
    {
        fixture.GoogleVerifier.SetIdentity("sub-new", "brand-new@example.com", "New User");

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest("any-id-token"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLogin_WhenEmailRegisteredButNotLinked_Returns202()
    {
        const string email = "existingnotlinked@example.com";
        await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest(email, "Password1!", "Alice"));

        fixture.GoogleVerifier.SetIdentity("sub-unlinked", email, "Alice");

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest("any-id-token"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLogin_WhenGoogleAccountAlreadyLinked_Returns200WithTokens()
    {
        const string email = "fastpath@example.com";
        const string subject = "sub-fastpath";

        // Seed: register user and link Google directly via domain
        await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest(email, "Password1!", "Alice"));

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var emailVal = Email.Create(email).Value;
            var user = await db.Users
                .Include(u => u.ExternalLogins)
                .FirstAsync(u => u.Email == emailVal);
            user.LinkExternalLogin(ExternalLoginProvider.Google, subject, clock.UtcNow);
            await db.SaveChangesAsync();
        }

        fixture.GoogleVerifier.SetIdentity(subject, email, "Alice");

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest("any-id-token"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GoogleLoginResponse>();
        Assert.NotNull(body);
        Assert.False(body.IsPending);
        Assert.NotNull(body.AccessToken);
        Assert.NotNull(body.RefreshToken);
    }

    [Fact]
    public async Task GoogleLogin_WhenTokenVerificationFails_Returns401()
    {
        fixture.GoogleVerifier.SetError(Error.Unauthorized("Users.ExternalLogin.InvalidIdToken", "invalid"));

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest("bad-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLogin_WhenIdTokenEmpty_Returns422()
    {
        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest(""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLogin_EmailLoop_CreatesPendingRecordInDatabase()
    {
        fixture.GoogleVerifier.SetIdentity("sub-pending", "pending@example.com", "Pending User");

        await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest("any-id-token"));

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var pending = await db.PendingExternalLogins
            .FirstOrDefaultAsync(p => p.Email == "pending@example.com");
        Assert.NotNull(pending);
        Assert.False(pending.IsExistingUser);
    }

    [Fact]
    public async Task GoogleLogin_WhenDisplayNameExceedsColumnLimit_TruncatesAndReturns202()
    {
        var overlong = new string('A', 150);
        fixture.GoogleVerifier.SetIdentity("sub-longname", "longname@example.com", overlong);

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest("any-id-token"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var pending = await db.PendingExternalLogins
            .FirstOrDefaultAsync(p => p.Email == "longname@example.com");
        Assert.NotNull(pending);
        Assert.Equal(100, pending.DisplayName.Length);
    }
}

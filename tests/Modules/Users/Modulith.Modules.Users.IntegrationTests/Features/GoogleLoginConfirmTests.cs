using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.ExternalLogin.Google.Confirm;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Features.ExternalLogin.Google.Login;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("GoogleUsersModule")]
[Trait("Category", "Integration")]
public sealed class GoogleLoginConfirmTests(GoogleUsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GoogleLoginConfirm_ForNewUser_Returns200AndCreatesUser()
    {
        const string email = "newgoogle@example.com";
        const string subject = "sub-new-confirm";

        var rawToken = await SeedPendingLoginAsync(subject, email, isExistingUser: false);

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GoogleLoginConfirmResponse>();
        Assert.NotNull(body);
        Assert.True(body.IsNewUser);
        Assert.NotEmpty(body.AccessToken);
        Assert.NotEmpty(body.RefreshToken);
        Assert.NotEqual(Guid.Empty, body.UserId);
    }

    [Fact]
    public async Task GoogleLoginConfirm_ForNewUser_PersistsUserInDatabase()
    {
        const string email = "newgoogle2@example.com";
        const string subject = "sub-new-confirm2";

        var rawToken = await SeedPendingLoginAsync(subject, email, isExistingUser: false);

        await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken));

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var emailVal = Email.Create(email).Value;
        var user = await db.Users
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.Email == emailVal);
        Assert.NotNull(user);
        Assert.Single(user.ExternalLogins);
        Assert.Equal("Google", user.ExternalLogins[0].Provider.ToString());
        Assert.Equal(subject, user.ExternalLogins[0].Subject);
    }

    [Fact]
    public async Task GoogleLoginConfirm_ForExistingUser_Returns200AndLinksGoogle()
    {
        const string email = "existinglinked@example.com";
        const string subject = "sub-existing-confirm";

        await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest(email, "Password1!", "Alice"));

        var rawToken = await SeedPendingLoginAsync(subject, email, isExistingUser: true);

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GoogleLoginConfirmResponse>();
        Assert.NotNull(body);
        Assert.False(body.IsNewUser);
        Assert.NotEmpty(body.AccessToken);
    }

    [Fact]
    public async Task GoogleLoginConfirm_ForExistingUser_LinksGoogleInDatabase()
    {
        const string email = "existinglinked2@example.com";
        const string subject = "sub-existing-confirm2";

        await _client.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest(email, "Password1!", "Alice"));

        var rawToken = await SeedPendingLoginAsync(subject, email, isExistingUser: true);

        await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken));

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var emailVal = Email.Create(email).Value;
        var user = await db.Users
            .Include(u => u.ExternalLogins)
            .FirstAsync(u => u.Email == emailVal);
        Assert.Single(user.ExternalLogins);
        Assert.Equal(subject, user.ExternalLogins[0].Subject);
    }

    [Fact]
    public async Task GoogleLoginConfirm_AfterLinking_SubsequentGoogleLoginUsesLinkedFastPath()
    {
        const string email = "fastpathafter@example.com";
        const string subject = "sub-fastpath-confirm";

        var rawToken = await SeedPendingLoginAsync(subject, email, isExistingUser: false);
        await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken));

        // Now the Google account is linked — fast path should return 200
        fixture.GoogleVerifier.SetIdentity(subject, email, "Test User");
        var loginResp = await _client.PostAsJsonAsync("/v1/users/auth/google/login",
            new GoogleLoginRequest("any-token"));

        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var body = await loginResp.Content.ReadFromJsonAsync<GoogleLoginResponse>();
        Assert.NotNull(body);
        Assert.False(body.IsPending);
    }

    [Fact]
    public async Task GoogleLoginConfirm_WithExpiredToken_Returns422()
    {
        const string email = "expired@example.com";
        const string subject = "sub-expired";

        var rawToken = await SeedPendingLoginAsync(subject, email, isExistingUser: false, lifetime: TimeSpan.FromSeconds(-1));

        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLoginConfirm_WithAlreadyConsumedToken_Returns422()
    {
        const string email = "consumed@example.com";
        const string subject = "sub-consumed";

        var rawToken = await SeedPendingLoginAsync(subject, email, isExistingUser: false);

        // Consume once successfully (user gets created)
        await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken));

        // Second attempt must fail
        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(rawToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLoginConfirm_WithUnknownToken_Returns422()
    {
        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest("totally-unknown-token-value"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GoogleLoginConfirm_WithEmptyToken_Returns422()
    {
        var response = await _client.PostAsJsonAsync("/v1/users/auth/google/confirm",
            new GoogleLoginConfirmRequest(""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private async Task<string> SeedPendingLoginAsync(
        string subject, string email, bool isExistingUser,
        TimeSpan? lifetime = null)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var (pending, rawToken) = PendingExternalLogin.Create(
            ExternalLoginProvider.Google, subject, email, "Test User",
            isExistingUser, createdFromIp: null, userAgent: null,
            lifetime ?? TimeSpan.FromMinutes(15), clock);

        db.PendingExternalLogins.Add(pending);
        await db.SaveChangesAsync();
        return rawToken;
    }
}

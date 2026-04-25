using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.Login;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Modulith.TestSupport;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("GoogleUsersModule")]
[Trait("Category", "Integration")]
public sealed class CompleteOnboardingTests(GoogleUsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _anon = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CompleteOnboarding_WithCorrectVersions_Returns204()
    {
        var (_, accessToken) = await SeedExternalUserAsync("onboard@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        var response = await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { termsVersion = "1.0", privacyPolicyVersion = "1.0" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_SetsHasCompletedOnboardingInDatabase()
    {
        const string email = "onboarddb@example.com";
        var (_, accessToken) = await SeedExternalUserAsync(email);
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { termsVersion = "1.0", privacyPolicyVersion = "1.0" });

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var emailVal = Email.Create(email).Value;
        var user = await db.Users.FirstAsync(u => u.Email == emailVal);
        Assert.True(user.HasCompletedOnboarding);
    }

    [Fact]
    public async Task CompleteOnboarding_RecordsTermsAcceptancesInDatabase()
    {
        const string email = "onboardterms@example.com";
        var (userId, accessToken) = await SeedExternalUserAsync(email);
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { termsVersion = "1.0", privacyPolicyVersion = "1.0" });

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var acceptances = await db.TermsAcceptances
            .Where(t => t.UserId == new UserId(userId))
            .ToListAsync();
        Assert.Equal(2, acceptances.Count);
        Assert.Contains(acceptances, t => string.Equals(t.Version, "tos:1.0", StringComparison.Ordinal));
        Assert.Contains(acceptances, t => string.Equals(t.Version, "pp:1.0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CompleteOnboarding_WithWrongTermsVersion_Returns422()
    {
        var (_, accessToken) = await SeedExternalUserAsync("onboardwrongtos@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        var response = await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { termsVersion = "0.9", privacyPolicyVersion = "1.0" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_WithWrongPrivacyPolicyVersion_Returns422()
    {
        var (_, accessToken) = await SeedExternalUserAsync("onboardwrongpp@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);

        var response = await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { termsVersion = "1.0", privacyPolicyVersion = "0.9" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_IsIdempotent_Returns204BothTimes()
    {
        var (_, accessToken) = await SeedExternalUserAsync("onboardidempotent@example.com");
        var auth = fixture.CreateAuthenticatedClientWithToken(accessToken);
        var body = new { termsVersion = "1.0", privacyPolicyVersion = "1.0" };

        var first = await auth.PostAsJsonAsync("/v1/users/me/onboarding", body);
        var second = await auth.PostAsJsonAsync("/v1/users/me/onboarding", body);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_WhenUnauthenticated_Returns401()
    {
        var response = await _anon.PostAsJsonAsync("/v1/users/me/onboarding",
            new { termsVersion = "1.0", privacyPolicyVersion = "1.0" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_AlsoWorksForPasswordUsers()
    {
        await _anon.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("pwduser@example.com", "Password1!", "Alice"));
        var login = await _anon.PostAsJsonAsync("/v1/users/login",
            new LoginRequest("pwduser@example.com", "Password1!"));
        var loginBody = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginBody);
        var auth = fixture.CreateAuthenticatedClientWithToken(loginBody.AccessToken);

        var response = await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { termsVersion = "1.0", privacyPolicyVersion = "1.0" });

        // Password users have HasCompletedOnboarding=true already but CompleteOnboarding is idempotent
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task<(Guid UserId, string AccessToken)> SeedExternalUserAsync(string email)
    {
        Guid userId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var emailVal = Email.Create(email).Value;
            var user = User.CreateExternal(emailVal, "ExternalUser", ExternalLoginProvider.Google, "sub-ext", clock).Value;
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id.Value;
        }

        var accessToken = ApiTestFixture.GenerateTestToken(userId, email, "ExternalUser");
        return (userId, accessToken);
    }
}

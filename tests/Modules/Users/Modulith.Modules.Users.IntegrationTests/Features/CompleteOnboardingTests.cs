using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.GetCurrentUser;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class CompleteOnboardingTests(UsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient anonymous = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CompleteOnboarding_WithAcceptTermsTrue_Returns204()
    {
        var (userId, email, displayName) = await RegisterUserAsync("onboard@example.com", "Onboard User");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        var response = await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptTerms = true, acceptMarketingEmails = false });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_WithAcceptTermsFalse_Returns422()
    {
        var (userId, email, displayName) = await RegisterUserAsync("onboardreject@example.com", "Reject User");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        var response = await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptTerms = false, acceptMarketingEmails = false });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_SetsHasCompletedOnboardingInDatabase()
    {
        var (userId, email, displayName) = await RegisterUserAsync("onboarddb@example.com", "Database User");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptTerms = true, acceptMarketingEmails = false });

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == new UserId(userId));
        Assert.True(user.HasCompletedOnboarding);
    }

    [Fact]
    public async Task CompleteOnboarding_RecordsSingleTosAcceptanceInDatabase()
    {
        var (userId, email, displayName) = await RegisterUserAsync("onboardterms@example.com", "Terms User");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptTerms = true, acceptMarketingEmails = false });

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var acceptances = await db.TermsAcceptances
            .Where(t => t.UserId == new UserId(userId))
            .ToListAsync();
        Assert.Single(acceptances);
        Assert.Equal("tos:1.0", acceptances[0].Version);
    }

    [Fact]
    public async Task CompleteOnboarding_WithMarketingConsent_RecordsConsentRow()
    {
        var (userId, email, displayName) = await RegisterUserAsync("onboardmarketing@example.com", "Marketing User");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptTerms = true, acceptMarketingEmails = true });

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var consent = await db.Consents
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ConsentKey == "notifications:marketing_email");
        Assert.NotNull(consent);
        Assert.True(consent.Granted);
        Assert.Equal("1.0", consent.PolicyVersion);
    }

    [Fact]
    public async Task CompleteOnboarding_WithoutMarketingConsent_DoesNotRecordConsentRow()
    {
        var (userId, email, displayName) = await RegisterUserAsync("onboardnomarketing@example.com", "No Marketing User");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptTerms = true, acceptMarketingEmails = false });

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var hasConsent = await db.Consents
            .AnyAsync(c => c.UserId == userId && c.ConsentKey == "notifications:marketing_email");
        Assert.False(hasConsent);
    }

    [Fact]
    public async Task CompleteOnboarding_IsIdempotent_Returns204BothTimes()
    {
        var (userId, email, displayName) = await RegisterUserAsync("onboardidempotent@example.com", "Idempotent User");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);
        var body = new { acceptTerms = true, acceptMarketingEmails = false };

        var first = await auth.PostAsJsonAsync("/v1/users/me/onboarding", body);
        var second = await auth.PostAsJsonAsync("/v1/users/me/onboarding", body);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_WhenUnauthenticated_Returns401()
    {
        var response = await anonymous.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptTerms = true, acceptMarketingEmails = false });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_AfterRegistration_ReturnsOnboardingIncomplete()
    {
        var (userId, email, displayName) = await RegisterUserAsync("pending-onboarding@example.com", "Pending User");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        var response = await auth.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetCurrentUserResponse>();
        Assert.NotNull(body);
        Assert.False(body.HasCompletedOnboarding);
    }

    private async Task<(Guid UserId, string Email, string DisplayName)> RegisterUserAsync(string email, string displayName)
    {
        var response = await anonymous.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest(email, "Password1!", displayName));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(body);

        return (body.UserId, email, displayName);
    }
}

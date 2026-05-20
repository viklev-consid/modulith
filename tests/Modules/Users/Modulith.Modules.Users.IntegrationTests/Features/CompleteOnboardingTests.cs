using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.GetOnboardingLegalRequirements;
using Modulith.Modules.Users.Features.GetCurrentUser;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class CompleteOnboardingTests(UsersApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient anonymous = fixture.CreateAnonymousClient();

    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
        await SeedLegalDocumentsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetOnboardingLegalRequirements_ReturnsRequiredDocumentsWithMarkdown()
    {
        var (userId, email, displayName) = await RegisterUserAsync("legaldocs@example.com", "Legal Docs User");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        var response = await auth.GetAsync("/v1/users/me/onboarding/legal-requirements");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetOnboardingLegalRequirementsResponse>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Documents.Count);
        Assert.Contains(body.Documents, d => string.Equals(d.Type, "termsOfService", StringComparison.Ordinal) && d.Markdown.Contains("Terms of Service", StringComparison.Ordinal));
        Assert.Contains(body.Documents, d => string.Equals(d.Type, "privacyPolicy", StringComparison.Ordinal) && d.Markdown.Contains("Privacy Policy", StringComparison.Ordinal));
        Assert.All(body.Documents, d => Assert.Equal(64, d.ContentHash.Length));
    }

    [Fact]
    public async Task CompleteOnboarding_WithoutAcceptedDocuments_Returns422()
    {
        var (userId, email, displayName) = await RegisterUserAsync("onboard@example.com", "Onboard User");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        var response = await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptMarketingEmails = false });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_WithEmptyAcceptedDocuments_Returns422()
    {
        var (userId, email, displayName) = await RegisterUserAsync("onboardreject@example.com", "Reject User");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        var response = await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptedDocuments = Array.Empty<object>(), acceptMarketingEmails = false });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_SetsHasCompletedOnboardingInDatabase()
    {
        var (userId, email, displayName) = await RegisterUserAsync("onboarddb@example.com", "Database User");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        var legalDocuments = await GetLegalDocumentPayloadsAsync();
        await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptedDocuments = legalDocuments, acceptMarketingEmails = false });

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == new UserId(userId));
        Assert.True(user.HasCompletedOnboarding);
    }

    [Fact]
    public async Task CompleteOnboarding_RecordsRequiredLegalAcceptancesInDatabase()
    {
        var (userId, email, displayName) = await RegisterUserAsync("onboardterms@example.com", "Terms User");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        var legalDocuments = await GetLegalDocumentPayloadsAsync();
        await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptedDocuments = legalDocuments, acceptMarketingEmails = false });

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var acceptances = await db.TermsAcceptances
            .Where(t => t.UserId == new UserId(userId))
            .OrderBy(t => t.Version)
            .ToListAsync();
        Assert.Equal(2, acceptances.Count);
        Assert.Contains(acceptances, a => string.Equals(a.Version, "tos:1.0", StringComparison.Ordinal) && a.DocumentType == LegalDocumentType.TermsOfService && a.ContentHash is not null);
        Assert.Contains(acceptances, a => string.Equals(a.Version, "privacy:1.0", StringComparison.Ordinal) && a.DocumentType == LegalDocumentType.PrivacyPolicy && a.ContentHash is not null);
    }

    [Fact]
    public async Task CompleteOnboarding_WithAcceptedDocuments_RecordsAcceptances()
    {
        var (userId, email, displayName) = await RegisterUserAsync("onboardaccepteddocs@example.com", "Accepted Docs User");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);
        var legalDocuments = await GetLegalDocumentPayloadsAsync();

        var response = await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptedDocuments = legalDocuments, acceptMarketingEmails = false });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_WithMissingRequiredDocument_Returns400()
    {
        var (userId, email, displayName) = await RegisterUserAsync("onboardmissingdoc@example.com", "Missing Docs User");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);
        var legalDocuments = (await GetLegalDocumentPayloadsAsync()).Take(1).ToArray();

        var response = await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptedDocuments = legalDocuments, acceptMarketingEmails = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_WithStaleDocumentHash_Returns400()
    {
        var (userId, email, displayName) = await RegisterUserAsync("onboardstaledoc@example.com", "Stale Docs User");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);
        var legalDocuments = await GetLegalDocumentPayloadsAsync();
        legalDocuments[0] = legalDocuments[0] with { ContentHash = new string('0', 64) };

        var response = await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptedDocuments = legalDocuments, acceptMarketingEmails = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_WithMarketingConsent_RecordsConsentRow()
    {
        var (userId, email, displayName) = await RegisterUserAsync("onboardmarketing@example.com", "Marketing User");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        var legalDocuments = await GetLegalDocumentPayloadsAsync();
        await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptedDocuments = legalDocuments, acceptMarketingEmails = true });

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

        var legalDocuments = await GetLegalDocumentPayloadsAsync();
        await auth.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptedDocuments = legalDocuments, acceptMarketingEmails = false });

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
        var legalDocuments = await GetLegalDocumentPayloadsAsync();
        var body = new { acceptedDocuments = legalDocuments, acceptMarketingEmails = false };

        var first = await auth.PostAsJsonAsync("/v1/users/me/onboarding", body);
        var second = await auth.PostAsJsonAsync("/v1/users/me/onboarding", body);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task CompleteOnboarding_WhenUnauthenticated_Returns401()
    {
        var response = await anonymous.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptMarketingEmails = false });

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

    private async Task SeedLegalDocumentsAsync()
    {
        await fixture.ExecuteDbAsync<UsersDbContext>(async (db, ct) =>
        {
            var now = fixture.Clock.UtcNow;
            db.LegalDocuments.Add(LegalDocument.Publish(
                LegalDocumentType.TermsOfService,
                "1.0",
                "Terms of Service",
                "# Terms of Service\n\nVersion 1.0\n",
                ComputeSha256("# Terms of Service\n\nVersion 1.0\n"),
                now,
                now,
                isRequiredForOnboarding: true));
            db.LegalDocuments.Add(LegalDocument.Publish(
                LegalDocumentType.PrivacyPolicy,
                "1.0",
                "Privacy Policy",
                "# Privacy Policy\n\nVersion 1.0\n",
                ComputeSha256("# Privacy Policy\n\nVersion 1.0\n"),
                now,
                now,
                isRequiredForOnboarding: true));
            await db.SaveChangesAsync(ct);
        });
    }

    private async Task<AcceptedLegalDocumentPayload[]> GetLegalDocumentPayloadsAsync()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var documents = await db.LegalDocuments
            .AsNoTracking()
            .Where(d => d.IsRequiredForOnboarding && d.SupersededAt == null)
            .OrderBy(d => d.DocumentType)
            .Select(d => new AcceptedLegalDocumentPayload(d.Id.Value, d.Version, d.ContentHash))
            .ToArrayAsync();

        return documents;
    }

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public sealed record AcceptedLegalDocumentPayload(Guid DocumentId, string Version, string ContentHash);
}

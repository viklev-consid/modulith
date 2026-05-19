using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.GetLegalCompliance;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class LegalComplianceTests(UsersApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetLegalCompliance_WhenNoContinuedUseDocumentsAreRequired_ReturnsCompliant()
    {
        await SeedLegalDocumentAsync("1.0", isRequiredForContinuedUse: false);
        var (userId, email, displayName) = await SeedUserAsync("legal-compliant@example.com", "Legal Compliant");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        var response = await auth.GetAsync("/v1/users/me/legal-compliance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetLegalComplianceResponse>();
        Assert.NotNull(body);
        Assert.True(body.IsCompliant);
        Assert.Equal("none", body.BlockingLevel);
        Assert.Empty(body.MissingDocuments);
    }

    [Fact]
    public async Task GetLegalCompliance_WhenNewRequiredVersionIsNotAccepted_ReturnsMissingDocument()
    {
        await SeedLegalDocumentAsync("1.1", isRequiredForContinuedUse: true);
        var (userId, email, displayName) = await SeedUserAsync("legal-missing@example.com", "Legal Missing");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        var response = await auth.GetAsync("/v1/users/me/legal-compliance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetLegalComplianceResponse>();
        Assert.NotNull(body);
        Assert.False(body.IsCompliant);
        Assert.Equal("blockAllAuthenticatedUse", body.BlockingLevel);
        var missing = Assert.Single(body.MissingDocuments);
        Assert.Equal("termsOfService", missing.Type);
        Assert.Equal("1.1", missing.Version);
    }

    [Fact]
    public async Task AcceptLegalDocuments_WhenCurrentRequiredVersionIsAccepted_MakesUserCompliant()
    {
        var document = await SeedLegalDocumentAsync("1.1", isRequiredForContinuedUse: true);
        var (userId, email, displayName) = await SeedUserAsync("legal-accept@example.com", "Legal Accept");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        var accept = await auth.PostAsJsonAsync("/v1/users/me/legal-acceptances",
            new
            {
                acceptedDocuments = new[]
                {
                    new { documentId = document.Id, document.Version, document.ContentHash },
                },
            });
        var compliance = await auth.GetAsync("/v1/users/me/legal-compliance");

        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);
        var body = await compliance.Content.ReadFromJsonAsync<GetLegalComplianceResponse>();
        Assert.NotNull(body);
        Assert.True(body.IsCompliant);
        Assert.Contains(body.AcceptedDocuments, d => string.Equals(d.Version, "tos:1.1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AcceptLegalDocuments_WithStaleHash_Returns400()
    {
        var document = await SeedLegalDocumentAsync("1.1", isRequiredForContinuedUse: true);
        var (userId, email, displayName) = await SeedUserAsync("legal-stale@example.com", "Legal Stale");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        var response = await auth.PostAsJsonAsync("/v1/users/me/legal-acceptances",
            new
            {
                acceptedDocuments = new[]
                {
                    new { documentId = document.Id, document.Version, ContentHash = new string('0', 64) },
                },
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LegalComplianceMiddleware_WhenBlockingDocumentIsMissing_BlocksNormalAuthenticatedActions()
    {
        await SeedLegalDocumentAsync("1.1", isRequiredForContinuedUse: true);
        var (userId, email, displayName) = await SeedUserAsync("legal-blocked@example.com", "Legal Blocked");
        var auth = fixture.CreateAuthenticatedClient(userId, email, displayName);

        var blocked = await auth.PatchAsJsonAsync("/v1/users/me/profile", new { displayName = "Still Blocked" });
        var compliance = await auth.GetAsync("/v1/users/me/legal-compliance");

        Assert.Equal((HttpStatusCode)428, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, compliance.StatusCode);
    }

    private async Task<SeededLegalDocument> SeedLegalDocumentAsync(string version, bool isRequiredForContinuedUse)
    {
        var markdown = $"# Terms of Service\n\nVersion {version}\n";
        var hash = ComputeSha256(markdown);

        return await fixture.QueryDbAsync<UsersDbContext, SeededLegalDocument>(async (db, ct) =>
        {
            var now = fixture.Clock.UtcNow;
            var document = LegalDocument.Publish(
                LegalDocumentType.TermsOfService,
                version,
                "Terms of Service",
                markdown,
                hash,
                now,
                now,
                isRequiredForOnboarding: false,
                isRequiredForContinuedUse: isRequiredForContinuedUse,
                continuedUseRequiredAt: now,
                blockingLevel: LegalDocumentBlockingLevel.BlockAllAuthenticatedUse);
            db.LegalDocuments.Add(document);
            await db.SaveChangesAsync(ct);

            return new SeededLegalDocument(document.Id.Value, document.Version, document.ContentHash);
        });
    }

    private async Task<(Guid UserId, string Email, string DisplayName)> SeedUserAsync(string email, string displayName)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        var user = User.CreateWithPassword(
            Email.Create(email).Value,
            new PasswordHash("hashed-password"),
            displayName).Value;

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (user.Id.Value, email, displayName);
    }

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record SeededLegalDocument(Guid Id, string Version, string ContentHash);
}

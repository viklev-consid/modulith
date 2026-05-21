using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.GetLegalDocument;
using Modulith.Modules.Users.Persistence;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersModule")]
[Trait("Category", "Integration")]
public sealed class GetLegalDocumentTests(UsersApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetLegalDocument_WhenCurrentVersionExists_ReturnsMarkdown()
    {
        var document = await SeedLegalDocumentAsync("1.1", superseded: false);
        var auth = fixture.CreateAuthenticatedClient(Guid.NewGuid(), "legal-doc@example.com", "Legal Doc");

        var response = await auth.GetAsync("/v1/users/legal-documents/termsOfService/1.1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromSeconds(300), response.Headers.CacheControl?.MaxAge);
        var body = await response.Content.ReadFromJsonAsync<GetLegalDocumentResponse>();
        Assert.NotNull(body);
        Assert.Equal(document.Id, body.Id);
        Assert.Equal("termsOfService", body.Type);
        Assert.Equal("1.1", body.Version);
        Assert.Equal(document.ContentHash, body.ContentHash);
        Assert.Contains("Version 1.1", body.Markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLegalDocument_WhenVersionIsSuperseded_Returns404()
    {
        await SeedLegalDocumentAsync("1.0", superseded: true);
        var auth = fixture.CreateAuthenticatedClient(Guid.NewGuid(), "legal-doc-superseded@example.com", "Legal Doc Superseded");

        var response = await auth.GetAsync("/v1/users/legal-documents/termsOfService/1.0");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(response.Headers.CacheControl);
    }

    [Fact]
    public async Task GetLegalDocument_WhenTypeIsUnknown_Returns404()
    {
        await SeedLegalDocumentAsync("1.1", superseded: false);
        var auth = fixture.CreateAuthenticatedClient(Guid.NewGuid(), "legal-doc-type@example.com", "Legal Doc Type");

        var response = await auth.GetAsync("/v1/users/legal-documents/cookiePolicy/1.1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(response.Headers.CacheControl);
    }

    [Fact]
    public async Task GetLegalDocument_WhenUnauthenticated_Returns401()
    {
        await SeedLegalDocumentAsync("1.1", superseded: false);
        var anonymous = fixture.CreateAnonymousClient();

        var response = await anonymous.GetAsync("/v1/users/legal-documents/termsOfService/1.1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<SeededLegalDocument> SeedLegalDocumentAsync(string version, bool superseded)
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
                isRequiredForContinuedUse: true,
                continuedUseRequiredAt: now,
                blockingLevel: LegalDocumentBlockingLevel.BlockAllAuthenticatedUse);

            if (superseded)
            {
                document.Supersede(now.AddMinutes(1));
            }

            db.LegalDocuments.Add(document);
            await db.SaveChangesAsync(ct);

            return new SeededLegalDocument(document.Id.Value, document.ContentHash);
        });
    }

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record SeededLegalDocument(Guid Id, string ContentHash);
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Features.ExportPersonalData;
using Modulith.Modules.Users.Features.Register;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Modulith.TestSupport;

namespace Modulith.Modules.Users.IntegrationTests.Features;

[Collection("UsersGdpr")]
[Trait("Category", "Integration")]
public sealed class ExportPersonalDataTests(GdprApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _anonymous = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExportPersonalData_Authenticated_ReturnsExports()
    {
        var registerResp = await (await _anonymous.PostAsJsonAsync("/v1/users/register",
            new RegisterRequest("alice@example.com", "Password1!", "Alice")))
            .Content.ReadFromJsonAsync<RegisterResponse>();

        var client = fixture.CreateAuthenticatedClient(
            registerResp!.UserId, "alice@example.com", "Alice");

        var response = await client.GetAsync("/v1/users/me/personal-data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ExportPersonalDataResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body.Exports);
        var usersExport = body.Exports.FirstOrDefault(e => string.Equals(e.ModuleName, "Users", StringComparison.Ordinal));
        Assert.NotNull(usersExport);
        Assert.Equal(registerResp.UserId, usersExport.UserId);
        Assert.True(usersExport.Data.ContainsKey("email"));
    }

    [Fact]
    public async Task ExportPersonalData_Unauthenticated_Returns401()
    {
        var response = await _anonymous.GetAsync("/v1/users/me/personal-data");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExportPersonalData_IncludesLinkedLogin_WithProviderSubjectAndLinkedAt()
    {
        const string email = "google-gdpr@example.com";
        const string subject = "google-sub-gdpr";
        var (_, accessToken) = await SeedExternalUserAsync(email, subject);
        var client = fixture.CreateAuthenticatedClientWithToken(accessToken);

        var response = await client.GetAsync("/v1/users/me/personal-data");
        var body = await response.Content.ReadFromJsonAsync<ExportPersonalDataResponse>();

        var usersExport = body!.Exports.First(e => string.Equals(e.ModuleName, "Users", StringComparison.Ordinal));
        var linkedLogins = ((JsonElement)usersExport.Data["linkedLogins"]!).EnumerateArray().ToList();

        Assert.Single(linkedLogins);
        Assert.Equal("Google", linkedLogins[0].GetProperty("provider").GetString());
        Assert.Equal(subject, linkedLogins[0].GetProperty("subject").GetString());
        Assert.NotEqual(default, linkedLogins[0].GetProperty("linkedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task ExportPersonalData_IncludesTermsAcceptance_WithVersionAndUserAgent()
    {
        const string email = "terms-gdpr@example.com";
        var (_, accessToken) = await SeedExternalUserAsync(email, "sub-terms-gdpr");
        var client = fixture.CreateAuthenticatedClientWithToken(accessToken);
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "TestBrowser/1.0");

        await client.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptTerms = true, acceptMarketingEmails = false });

        var response = await client.GetAsync("/v1/users/me/personal-data");
        var body = await response.Content.ReadFromJsonAsync<ExportPersonalDataResponse>();

        var usersExport = body!.Exports.First(e => string.Equals(e.ModuleName, "Users", StringComparison.Ordinal));
        var acceptances = ((JsonElement)usersExport.Data["termsAcceptances"]!).EnumerateArray().ToList();

        Assert.Single(acceptances);
        Assert.Equal("tos:1.0", acceptances[0].GetProperty("version").GetString());
        Assert.NotEqual(default, acceptances[0].GetProperty("acceptedAt").GetDateTimeOffset());
        Assert.Equal("TestBrowser/1.0", acceptances[0].GetProperty("userAgent").GetString());
    }

    [Fact]
    public async Task ExportPersonalData_IncludesConsent_WithGrantedUserAgent()
    {
        const string email = "consent-gdpr@example.com";
        var (_, accessToken) = await SeedExternalUserAsync(email, "sub-consent-gdpr");
        var client = fixture.CreateAuthenticatedClientWithToken(accessToken);
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "ConsentBrowser/2.0");

        await client.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptTerms = true, acceptMarketingEmails = true });

        var response = await client.GetAsync("/v1/users/me/personal-data");
        var body = await response.Content.ReadFromJsonAsync<ExportPersonalDataResponse>();

        var usersExport = body!.Exports.First(e => string.Equals(e.ModuleName, "Users", StringComparison.Ordinal));
        var consents = ((JsonElement)usersExport.Data["consents"]!).EnumerateArray().ToList();
        var marketingConsent = consents.Single(c =>
            string.Equals(c.GetProperty("consentKey").GetString(), "notifications:marketing_email", StringComparison.Ordinal));

        Assert.True(marketingConsent.GetProperty("granted").GetBoolean());
        Assert.Equal("ConsentBrowser/2.0", marketingConsent.GetProperty("grantedUserAgent").GetString());
    }

    [Fact]
    public async Task ExportPersonalData_HasCompletedOnboarding_FalseBeforeOnboarding()
    {
        var (_, accessToken) = await SeedExternalUserAsync("preboard-export@example.com", "sub-preboard-export");
        var client = fixture.CreateAuthenticatedClientWithToken(accessToken);

        var response = await client.GetAsync("/v1/users/me/personal-data");
        var body = await response.Content.ReadFromJsonAsync<ExportPersonalDataResponse>();

        var usersExport = body!.Exports.First(e => string.Equals(e.ModuleName, "Users", StringComparison.Ordinal));
        Assert.False(((JsonElement)usersExport.Data["hasCompletedOnboarding"]!).GetBoolean());
    }

    [Fact]
    public async Task ExportPersonalData_HasCompletedOnboarding_TrueAfterOnboarding()
    {
        var (_, accessToken) = await SeedExternalUserAsync("postboard-export@example.com", "sub-postboard-export");
        var client = fixture.CreateAuthenticatedClientWithToken(accessToken);

        await client.PostAsJsonAsync("/v1/users/me/onboarding",
            new { acceptTerms = true, acceptMarketingEmails = false });

        var response = await client.GetAsync("/v1/users/me/personal-data");
        var body = await response.Content.ReadFromJsonAsync<ExportPersonalDataResponse>();

        var usersExport = body!.Exports.First(e => string.Equals(e.ModuleName, "Users", StringComparison.Ordinal));
        Assert.True(((JsonElement)usersExport.Data["hasCompletedOnboarding"]!).GetBoolean());
    }

    private async Task<(Guid UserId, string AccessToken)> SeedExternalUserAsync(string email, string subject)
    {
        Guid userId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var emailVal = Email.Create(email).Value;
            var user = User.CreateExternal(emailVal, "ExternalUser", ExternalLoginProvider.Google, subject, clock).Value;
            user.LinkExternalLogin(ExternalLoginProvider.Google, subject, clock.UtcNow);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id.Value;
        }

        var accessToken = ApiTestFixture.GenerateTestToken(userId, email, "ExternalUser");
        return (userId, accessToken);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Modulith.Modules.Users.Features.Register;

namespace Modulith.SmokeTests;

[Collection("Smoke")]
[Trait("Category", "Smoke")]
public sealed class SmokeTests(SmokeTestFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── 10.1a ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StackBoots_HealthEndpointReturns200()
    {
        var response = await client.GetAsync("/alive");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── 10.1b ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterLoginGetMe_FullPipeline()
    {
        // Register
        var registerResponse = await client.PostAsJsonAsync(
            "/v1/users/register",
            new RegisterRequest("smoke@example.com", "Password1!", "Smoke User"));

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var registered = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(registered);
        Assert.NotEmpty(registered.AccessToken);

        // Get /me with the issued token
        var authed = fixture.CreateAuthenticatedClientWithToken(registered.AccessToken);
        var meResponse = await authed.GetAsync("/v1/users/me");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var me = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("smoke@example.com", me.GetProperty("email").GetString());
    }

    // ── 10.1c ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WelcomeEmailArrivesInMailpit_AfterRegister()
    {
        using var http = new HttpClient();
        await http.DeleteAsync($"{fixture.MailpitApiUrl}/api/v1/messages");

        const string email = "notify@example.com";
        await client.PostAsJsonAsync(
            "/v1/users/register",
            new RegisterRequest(email, "Password1!", "Notify User"));

        // Wolverine processes UserRegisteredV1 asynchronously — poll for up to 10 s.
        MailpitMessagesResponse? result = null;

        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));

            result = await http.GetFromJsonAsync<MailpitMessagesResponse>(
                $"{fixture.MailpitApiUrl}/api/v1/messages");

            if (result?.Messages?.Any(m => string.Equals(m.Subject, "Welcome to Modulith!", StringComparison.Ordinal)) == true)
            {
                break;
            }
        }

        Assert.NotNull(result);
        Assert.Contains(result.Messages ?? [], m =>
            string.Equals(m.Subject, "Welcome to Modulith!", StringComparison.Ordinal));
    }

    // ── 10.1d ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OpenApiDocument_GeneratesSuccessfully()
    {
        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("3.1.1", doc.GetProperty("openapi").GetString());
        Assert.True(doc.TryGetProperty("paths", out _), "OpenAPI document should contain paths.");
    }

    // ── Mailpit API response shapes ───────────────────────────────────────────

    private sealed record MailpitMessagesResponse(MailpitMessage[]? Messages, int Total);
    private sealed record MailpitMessage(MailpitAddress[]? To, string? Subject);
    private sealed record MailpitAddress(string Address);
}

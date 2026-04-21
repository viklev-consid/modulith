using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Modulith.Modules.Users.Features.Register;

namespace Modulith.SmokeTests;

[Collection("Smoke")]
[Trait("Category", "Smoke")]
public sealed class SmokeTests(SmokeTestFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── 10.1a ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StackBoots_HealthEndpointReturns200()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── 10.1b ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterLoginGetMe_FullPipeline()
    {
        // Register
        var registerResponse = await _client.PostAsJsonAsync(
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
        await _client.PostAsJsonAsync(
            "/v1/users/register",
            new RegisterRequest("notify@example.com", "Password1!", "Notify User"));

        // Wolverine processes UserRegisteredV1 asynchronously — poll for up to 10 s.
        using var http = new HttpClient();
        MailpitMessagesResponse? result = null;

        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));

            result = await http.GetFromJsonAsync<MailpitMessagesResponse>(
                $"{fixture.MailpitApiUrl}/api/v1/messages");

            if (result?.Messages?.Length > 0)
            {
                break;
            }
        }

        Assert.NotNull(result);
        Assert.True(result.Messages?.Length > 0, "Expected at least one email in Mailpit after registration.");

        var msg = result.Messages![0];
        Assert.Contains("notify@example.com", msg.To?.Select(t => t.Address) ?? []);
    }

    // ── 10.1d ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OpenApiDocument_GeneratesSuccessfully()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("3.0.1", doc.GetProperty("openapi").GetString());
        Assert.True(doc.TryGetProperty("paths", out _), "OpenAPI document should contain paths.");
    }

    // ── Mailpit API response shapes ───────────────────────────────────────────

    private sealed record MailpitMessagesResponse(MailpitMessage[]? Messages, int Total);
    private sealed record MailpitMessage(MailpitAddress[]? To, string? Subject);
    private sealed record MailpitAddress(string Address);
}

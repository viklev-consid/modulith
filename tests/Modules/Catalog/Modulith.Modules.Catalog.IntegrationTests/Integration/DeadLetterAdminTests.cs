using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Users.Persistence;
using Wolverine;
using Wolverine.Tracking;

namespace Modulith.Modules.Catalog.IntegrationTests.Integration;

/// <summary>
/// Integration tests for <c>/v1/admin/dead-letters</c>.
/// Uses <see cref="WolverineDeadLetterFixture"/> because it injects
/// <see cref="AlwaysThrowConsentRegistry"/>, which guarantees exactly one dead letter
/// is produced per user registration — making the test state deterministic.
/// </summary>
[Collection("WolverineDeadLetter")]
[Trait("Category", "Integration")]
public sealed class DeadLetterAdminTests(WolverineDeadLetterFixture fixture) : IAsyncLifetime
{
    // Admin client — role "admin" passes the Admin authorization policy.
    private readonly HttpClient adminClient = fixture.CreateAuthenticatedClient(
        Guid.NewGuid(), "dlq-admin@example.com", "DLQ Admin", "admin");

    // Normal user client — should be rejected by the Admin policy.
    private readonly HttpClient userClient = fixture.CreateAuthenticatedClient(
        Guid.NewGuid(), "dlq-user@example.com", "DLQ User", "user");

    // Unauthenticated client — should receive 401.
    private readonly HttpClient anonClient = fixture.CreateAnonymousClient();

    // Separate client for producing dead letters so headers don't bleed.
    private readonly HttpClient registerClient = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Registers a user and waits for the Wolverine pipeline to settle.
    /// AlwaysThrowConsentRegistry causes the Notifications handler to throw
    /// InvalidOperationException, which the MoveToErrorQueue policy moves to the
    /// dead-letter table — exactly one dead letter per call.
    /// </summary>
    private async Task ProduceDeadLetterAsync()
    {
        var request = new { Email = $"dlq-{Guid.NewGuid():N}@example.com", Password = "Password1!", DisplayName = "DLQ Test" };
        Func<IMessageContext, Task> act = async _ =>
        {
            var response = await registerClient.PostAsJsonAsync("/v1/users/register", request);
            response.EnsureSuccessStatusCode();
        };
        await fixture.ApplicationHost.TrackActivity()
            .DoNotAssertOnExceptionsDetected()
            .Timeout(TimeSpan.FromSeconds(15))
            .ExecuteAndWaitAsync(act);
    }

    // ── Authorization surface ─────────────────────────────────────────────────

    [Fact]
    public async Task ListDeadLetters_AnonymousRequest_Returns401()
    {
        var response = await anonClient.GetAsync("/v1/admin/dead-letters");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListDeadLetters_UserRoleToken_Returns403()
    {
        var response = await userClient.GetAsync("/v1/admin/dead-letters");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── List ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListDeadLetters_AfterFailedMessage_ReturnsEntry()
    {
        await ProduceDeadLetterAsync();

        var response = await adminClient.GetAsync("/v1/admin/dead-letters");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 1);
        Assert.NotEmpty(body.GetProperty("envelopes").EnumerateArray());
    }

    // ── Get by ID ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDeadLetterById_ExistingId_Returns200WithEnvelope()
    {
        await ProduceDeadLetterAsync();

        var list = await adminClient.GetFromJsonAsync<JsonElement>("/v1/admin/dead-letters");
        var id = list.GetProperty("envelopes")[0].GetProperty("id").GetString();
        Assert.NotNull(id);

        var response = await adminClient.GetAsync($"/v1/admin/dead-letters/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, envelope.GetProperty("id").GetString());
        Assert.False(string.IsNullOrEmpty(envelope.GetProperty("exceptionType").GetString()));
    }

    [Fact]
    public async Task GetDeadLetterById_UnknownId_Returns404()
    {
        var response = await adminClient.GetAsync($"/v1/admin/dead-letters/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Replay ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReplayDeadLetters_ByMessageIds_Returns202()
    {
        await ProduceDeadLetterAsync();

        var list = await adminClient.GetFromJsonAsync<JsonElement>("/v1/admin/dead-letters");
        var ids = list.GetProperty("envelopes")
            .EnumerateArray()
            .Select(e => Guid.Parse(e.GetProperty("id").GetString()!))
            .ToArray();

        var response = await adminClient.PostAsJsonAsync("/v1/admin/dead-letters/replay", new { messageIds = ids });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    // ── Discard ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DiscardDeadLetters_ByMessageIds_RemovesEntries()
    {
        await ProduceDeadLetterAsync();

        var list = await adminClient.GetFromJsonAsync<JsonElement>("/v1/admin/dead-letters");
        var ids = list.GetProperty("envelopes")
            .EnumerateArray()
            .Select(e => Guid.Parse(e.GetProperty("id").GetString()!))
            .ToArray();
        Assert.NotEmpty(ids);

        var discardResponse = await adminClient.PostAsJsonAsync("/v1/admin/dead-letters/discard", new { messageIds = ids });
        Assert.Equal(HttpStatusCode.NoContent, discardResponse.StatusCode);

        var afterList = await adminClient.GetFromJsonAsync<JsonElement>("/v1/admin/dead-letters");
        Assert.Equal(0, afterList.GetProperty("totalCount").GetInt32());
    }

    // ── Retention configuration ───────────────────────────────────────────────

    [Fact]
    public void DeadLetterRetention_IsEnabledWithThirtyDayExpiration()
    {
        var opts = fixture.Services.GetRequiredService<WolverineOptions>();

        Assert.True(opts.Durability.DeadLetterQueueExpirationEnabled,
            "Dead-letter expiration must be enabled so the table is bounded in size.");
        Assert.Equal(TimeSpan.FromDays(30), opts.Durability.DeadLetterQueueExpiration);
    }
}

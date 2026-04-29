using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Wolverine;
using Wolverine.Tracking;

namespace Modulith.Modules.Catalog.IntegrationTests.Integration;

[Collection("CrossModule")]
[Trait("Category", "Integration")]
public sealed class OnUserErasureRequestedTests(CrossModuleApiFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UserErasureRequested_AnonymizesCatalogCustomer()
    {
        // Arrange — register user so a Catalog customer exists
        var request = new { Email = "erasure-catalog@example.com", Password = "Password1!", DisplayName = "Erasure Catalog" };
        Guid userId = Guid.Empty;

        Func<IMessageContext, Task> register = async _ =>
        {
            var resp = await _client.PostAsJsonAsync("/v1/users/register", request);
            var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
            userId = body!.RootElement.GetProperty("userId").GetGuid();
        };
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(register);

        // Verify customer was created
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var customer = await db.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
            Assert.NotNull(customer);
        }

        // Act — deliver UserErasureRequestedV1
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .InvokeMessageAndWaitAsync(new UserErasureRequestedV1(userId, "Erasure Catalog", Guid.NewGuid()));

        // Assert — customer should be anonymized
        using var assertScope = fixture.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var anonymized = await assertDb.Customers.FirstOrDefaultAsync(c => c.UserId == userId);

        Assert.NotNull(anonymized);
        Assert.Equal("deleted@example.com", anonymized.Email);
        Assert.Equal("Deleted User", anonymized.DisplayName);
    }

    [Fact]
    public async Task UserErasureRequested_NoCustomer_IsNoOp()
    {
        // Act — deliver erasure for a user that never registered (or was already erased)
        var unknownUserId = Guid.NewGuid();

        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .InvokeMessageAndWaitAsync(new UserErasureRequestedV1(unknownUserId, null, Guid.NewGuid()));

        // Assert — no customer row was created as a side effect
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var count = await db.Customers.CountAsync(c => c.UserId == unknownUserId);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task UserErasureRequested_ReplayIsSafe()
    {
        // Arrange — register user
        var request = new { Email = "erasure-catalog-replay@example.com", Password = "Password1!", DisplayName = "Replay" };
        Guid userId = Guid.Empty;

        Func<IMessageContext, Task> register = async _ =>
        {
            var resp = await _client.PostAsJsonAsync("/v1/users/register", request);
            var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
            userId = body!.RootElement.GetProperty("userId").GetGuid();
        };
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(register);

        // First delivery
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .InvokeMessageAndWaitAsync(new UserErasureRequestedV1(userId, "Replay", Guid.NewGuid()));

        // Second delivery — already anonymized, must not throw
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .InvokeMessageAndWaitAsync(new UserErasureRequestedV1(userId, "Replay", Guid.NewGuid()));

        // Assert — still anonymized, no reversion
        using var assertScope = fixture.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var customer = await assertDb.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
        Assert.NotNull(customer);
        Assert.Equal("deleted@example.com", customer.Email);
    }
}

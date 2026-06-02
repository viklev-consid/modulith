using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Wolverine;
using Wolverine.Tracking;

namespace Modulith.Modules.Audit.IntegrationTests.Integration;

[Collection("AuditCrossModule")]
[Trait("Category", "Integration")]
public sealed class OnUserErasureRequestedTests(AuditCrossModuleFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UserErasureRequested_AnonymizesAuditEntries()
    {
        // Arrange — register a user so audit entries exist for them
        var request = new { Email = "erasure-audit@example.com", Password = "Password1!", DisplayName = "Erasure Audit" };

        Func<IMessageContext, Task> register = async _ =>
        {
            await client.PostAsJsonAsync("/v1/users/register", request);
        };
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(register);

        // Capture the userId from the audit entry created by registration
        Guid userId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            var entry = await db.AuditEntries
                .FirstOrDefaultAsync(e => e.EventType == "user.registered");
            Assert.NotNull(entry);
            userId = entry.ActorId!.Value;
        }

        // Act — deliver UserErasureRequestedV1
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .InvokeMessageAndWaitAsync(new UserErasureRequestedV1(userId, "Erasure Audit", Guid.NewGuid()));

        // Assert — audit entries referencing the user should be anonymized (ActorId nulled)
        using var assertScope = fixture.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var remaining = await assertDb.AuditEntries
            .Where(e => e.ActorId == userId || e.ResourceId == userId)
            .ToListAsync();

        Assert.Empty(remaining);
    }

    [Fact]
    public async Task UserErasureRequested_ReplayIsSafe()
    {
        // Arrange — register so audit entries exist
        var request = new { Email = "erasure-audit-replay@example.com", Password = "Password1!", DisplayName = "Replay" };

        Func<IMessageContext, Task> register = async _ =>
        {
            await client.PostAsJsonAsync("/v1/users/register", request);
        };
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(register);

        Guid userId;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            var entry = await db.AuditEntries
                .FirstOrDefaultAsync(e => e.EventType == "user.registered");
            Assert.NotNull(entry);
            userId = entry.ActorId!.Value;
        }

        // First delivery
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .InvokeMessageAndWaitAsync(new UserErasureRequestedV1(userId, "Replay", Guid.NewGuid()));

        // Second delivery — already anonymized, must not throw
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .InvokeMessageAndWaitAsync(new UserErasureRequestedV1(userId, "Replay", Guid.NewGuid()));

        // Assert — state is still correctly anonymized after replay
        using var assertScope = fixture.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var remaining = await assertDb.AuditEntries
            .Where(e => e.ActorId == userId || e.ResourceId == userId)
            .ToListAsync();

        Assert.Empty(remaining);
    }

    [Fact]
    public async Task UserErasureRequested_RedactsNestedPayloadUserIdsAndPersonalData()
    {
        var userId = Guid.NewGuid();

        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            db.AuditEntries.Add(Domain.AuditEntry.Create(
                "user.test",
                userId,
                "User",
                userId,
                $$"""{"userId":"{{userId}}","nested":{"changedBy":"{{userId}}","email":"private@example.com"},"ids":["{{userId}}"],"count":1}""",
                DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .InvokeMessageAndWaitAsync(new UserErasureRequestedV1(userId, "Erase Me", Guid.NewGuid()));

        using var assertScope = fixture.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var entry = await assertDb.AuditEntries.SingleAsync(e => e.EventType == "user.test");
        using var payload = JsonDocument.Parse(entry.Payload);

        Assert.Equal("[REDACTED]", payload.RootElement.GetProperty("userId").GetString());
        Assert.Equal("[REDACTED]", payload.RootElement.GetProperty("nested").GetProperty("changedBy").GetString());
        Assert.Equal("[REDACTED]", payload.RootElement.GetProperty("nested").GetProperty("email").GetString());
        Assert.Equal("[REDACTED]", payload.RootElement.GetProperty("ids")[0].GetString());
        Assert.Equal(1, payload.RootElement.GetProperty("count").GetInt32());
    }
}

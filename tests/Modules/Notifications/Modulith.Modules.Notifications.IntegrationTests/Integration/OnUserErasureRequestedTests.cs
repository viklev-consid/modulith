using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Wolverine;
using Wolverine.Tracking;

namespace Modulith.Modules.Notifications.IntegrationTests.Integration;

[Collection("NotificationsCrossModule")]
[Trait("Category", "Integration")]
public sealed class OnUserErasureRequestedTests(NotificationsCrossModuleFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task UserErasureRequested_DeletesNotificationLogs()
    {
        // Arrange — register user so notification logs exist (welcome email)
        var request = new { Email = "erasure-notif@example.com", Password = "Password1!", DisplayName = "Erasure Notif" };
        Guid userId = Guid.Empty;

        Func<IMessageContext, Task> register = async _ =>
        {
            var resp = await client.PostAsJsonAsync("/v1/users/register", request);
            var body = await resp.Content.ReadFromJsonAsync<JsonDocument>();
            userId = body!.RootElement.GetProperty("userId").GetGuid();
        };
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .ExecuteAndWaitAsync(register);

        // Verify notification log was created
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            var count = await db.NotificationLogs.CountAsync(l => l.UserId == userId);
            Assert.True(count > 0, "Expected at least one notification log for the registered user.");
        }

        // Act — deliver UserErasureRequestedV1
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .InvokeMessageAndWaitAsync(new UserErasureRequestedV1(userId, "Erasure Notif", Guid.NewGuid()));

        // Assert — notification logs for the user should be deleted
        using var assertScope = fixture.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var remaining = await assertDb.NotificationLogs.CountAsync(l => l.UserId == userId);

        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task UserErasureRequested_NoLogs_IsNoOp()
    {
        // Act — deliver erasure for a user that has no notification logs
        var unknownUserId = Guid.NewGuid();

        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .InvokeMessageAndWaitAsync(new UserErasureRequestedV1(unknownUserId, null, Guid.NewGuid()));

        // Assert — no logs created as side effect
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var count = await db.NotificationLogs.CountAsync(l => l.UserId == unknownUserId);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task UserErasureRequested_ReplayIsSafe()
    {
        // Arrange — register user
        var request = new { Email = "erasure-notif-replay@example.com", Password = "Password1!", DisplayName = "Replay" };
        Guid userId = Guid.Empty;

        Func<IMessageContext, Task> register = async _ =>
        {
            var resp = await client.PostAsJsonAsync("/v1/users/register", request);
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

        // Second delivery — logs already deleted, must not throw
        await fixture.ApplicationHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(10))
            .InvokeMessageAndWaitAsync(new UserErasureRequestedV1(userId, "Replay", Guid.NewGuid()));

        // Assert — logs are gone, no resurrection
        using var assertScope = fixture.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var remaining = await assertDb.NotificationLogs.CountAsync(l => l.UserId == userId);
        Assert.Equal(0, remaining);
    }
}

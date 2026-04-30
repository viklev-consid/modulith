using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Integration.Subscribers;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;
using Wolverine.Tracking;

namespace Modulith.Modules.Notifications.IntegrationTests.Integration;

/// <summary>
/// Proves that <c>NotificationSendGuard.MarkReadyAsync</c> enables transient SMTP recovery
/// without relying on the stale-row clock path in <c>TryClaimAsync</c>.
/// </summary>
[Collection("NotificationsRecovery")]
[Trait("Category", "Integration")]
public sealed class NotificationsSendGuardRecoveryTests(NotificationsRecoveryFixture fixture) : IAsyncLifetime
{
    private readonly HttpClient _client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Verifies the end-to-end recovery path introduced by <c>MarkReadyAsync</c>.
    /// <para>
    /// <c>FlakyEmailSender</c> is configured with zero clock advance: the stale-row recovery
    /// path in <c>TryClaimAsync</c> cannot fire on the Wolverine retry because the row has only
    /// been Sending for a fraction of a second, well below the 5-minute threshold.
    /// Without <c>MarkReadyAsync</c> the retry would see a non-stale Sending row and return
    /// <c>false</c>, silently skipping the send. With it, the row is reset to Pending
    /// immediately in the <c>catch (IOException)</c> block so the retry re-claims and succeeds.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TransientSmtpFailure_WithMarkReadyAsync_RetriesAndDelivers()
    {
        var request = new { Email = "recovery-test@example.com", Password = "Password1!", DisplayName = "Recovery Test" };

        // Act — TrackActivity waits for all cascading messages to settle, including the retry.
        // DoNotAssertOnExceptionsDetected is required because TrackActivity records the
        // intermediate IOException even though the message ultimately succeeds.
        Func<IMessageContext, Task> act = async _ =>
        {
            var response = await _client.PostAsJsonAsync("/v1/users/register", request);
            response.EnsureSuccessStatusCode();
        };
        await fixture.ApplicationHost.TrackActivity()
            .DoNotAssertOnExceptionsDetected()
            .Timeout(TimeSpan.FromSeconds(30))
            .ExecuteAndWaitAsync(act);

        // Email delivered exactly once on the successful retry
        Assert.Single(fixture.FlakyEmail.SentMessages);
        Assert.Equal(2, fixture.FlakyEmail.TotalAttempts);

        // Notification log must be Sent (not stuck in Sending)
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var log = await db.NotificationLogs
            .SingleAsync(l => l.RecipientEmail == "recovery-test@example.com");
        Assert.Equal(NotificationDeliveryStatus.Sent, log.DeliveryStatus);
    }

    /// <summary>
    /// Unit-style integration test for the <c>NotificationSendGuard</c> state machine:
    /// verifies that <c>MarkReadyAsync</c> resets a <c>Sending</c> row back to <c>Pending</c>
    /// so that a subsequent <c>TryClaimAsync</c> can re-claim it.
    /// </summary>
    [Fact]
    public async Task MarkReadyAsync_AfterClaim_ResetsRowToPendingAndAllowsReClaim()
    {
        // Arrange — insert a Pending notification log directly
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var guard = scope.ServiceProvider.GetRequiredService<NotificationSendGuard>();

        var idempotencyKey = Guid.NewGuid();
        var log = NotificationLog.Create(
            Guid.NewGuid(), "guard-test@example.com", NotificationType.WelcomeEmail,
            "Subject", clock.UtcNow, idempotencyKey);
        db.NotificationLogs.Add(log);
        await db.SaveChangesAsync();

        // Act 1 — claim the send slot (Pending → Sending)
        var claimed = await guard.TryClaimAsync(idempotencyKey, CancellationToken.None);
        Assert.True(claimed, "First TryClaimAsync must succeed on a Pending row");

        var afterClaim = await db.NotificationLogs
            .AsNoTracking()
            .SingleAsync(l => l.IdempotencyKey == idempotencyKey);
        Assert.Equal(NotificationDeliveryStatus.Sending, afterClaim.DeliveryStatus);
        Assert.NotNull(afterClaim.SendingClaimedAt);

        // Act 2 — MarkReadyAsync resets back to Pending (simulates the catch-IOException path)
        await guard.MarkReadyAsync(idempotencyKey, CancellationToken.None);

        var afterReset = await db.NotificationLogs
            .AsNoTracking()
            .SingleAsync(l => l.IdempotencyKey == idempotencyKey);
        Assert.Equal(NotificationDeliveryStatus.Pending, afterReset.DeliveryStatus);
        Assert.Null(afterReset.SendingClaimedAt);

        // Act 3 — second TryClaimAsync must succeed because the row is Pending again
        var reClaimed = await guard.TryClaimAsync(idempotencyKey, CancellationToken.None);
        Assert.True(reClaimed, "TryClaimAsync must succeed after MarkReadyAsync resets the row");

        var afterReClaim = await db.NotificationLogs
            .AsNoTracking()
            .SingleAsync(l => l.IdempotencyKey == idempotencyKey);
        Assert.Equal(NotificationDeliveryStatus.Sending, afterReClaim.DeliveryStatus);
    }
}

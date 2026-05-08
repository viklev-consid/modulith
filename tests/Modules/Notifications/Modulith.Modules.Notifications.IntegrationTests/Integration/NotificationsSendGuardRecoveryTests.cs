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
    private readonly HttpClient client = fixture.CreateAnonymousClient();

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Verifies the end-to-end recovery path driven by <c>MarkReadyAsync</c>.
    /// <para>
    /// <c>FlakyEmailSender</c> throws <see cref="RetryableSmtpException"/> on the first call.
    /// The handler's <c>catch (RetryableSmtpException)</c> block calls
    /// <c>NotificationSendGuard.MarkReadyAsync</c>, which resets <c>Sending → Pending</c>
    /// immediately so the Wolverine retry can re-claim without waiting for the 5-minute
    /// stale-row threshold.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TransientSmtpFailure_WithMarkReadyAsync_RetriesAndDelivers()
    {
        var request = new { Email = "recovery-test@example.com", Password = "Password1!", DisplayName = "Recovery Test" };

        // DoNotAssertOnExceptionsDetected is required because TrackActivity records the
        // intermediate RetryableSmtpException even though the message ultimately succeeds.
        Func<IMessageContext, Task> act = async _ =>
        {
            var response = await client.PostAsJsonAsync("/v1/users/register", request);
            response.EnsureSuccessStatusCode();
        };
        await fixture.ApplicationHost.TrackActivity()
            .DoNotAssertOnExceptionsDetected()
            .Timeout(TimeSpan.FromSeconds(30))
            .ExecuteAndWaitAsync(act);

        // Email delivered exactly once on the successful retry.
        Assert.Single(fixture.FlakyEmail.SentMessages);
        Assert.Equal(2, fixture.FlakyEmail.TotalAttempts);

        // Notification log must be Sent (not stuck in Sending).
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
        // Arrange — insert a Pending notification log directly.
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

        // Act 1 — claim the send slot (Pending → Sending).
        var firstToken = await guard.TryClaimAsync(idempotencyKey, CancellationToken.None);
        Assert.NotNull(firstToken);

        var afterClaim = await db.NotificationLogs
            .AsNoTracking()
            .SingleAsync(l => l.IdempotencyKey == idempotencyKey);
        Assert.Equal(NotificationDeliveryStatus.Sending, afterClaim.DeliveryStatus);
        Assert.NotNull(afterClaim.SendingClaimedAt);

        // Act 2 — MarkReadyAsync resets back to Pending (simulates the catch-RetryableSmtpException path).
        await guard.MarkReadyAsync(idempotencyKey, firstToken.Value, CancellationToken.None);

        var afterReset = await db.NotificationLogs
            .AsNoTracking()
            .SingleAsync(l => l.IdempotencyKey == idempotencyKey);
        Assert.Equal(NotificationDeliveryStatus.Pending, afterReset.DeliveryStatus);
        Assert.Null(afterReset.SendingClaimedAt);

        // Act 3 — second TryClaimAsync must succeed because the row is Pending again.
        var secondToken = await guard.TryClaimAsync(idempotencyKey, CancellationToken.None);
        Assert.NotNull(secondToken);

        var afterReClaim = await db.NotificationLogs
            .AsNoTracking()
            .SingleAsync(l => l.IdempotencyKey == idempotencyKey);
        Assert.Equal(NotificationDeliveryStatus.Sending, afterReClaim.DeliveryStatus);
    }

    /// <summary>
    /// Verifies the DLQ replay recovery path: <c>TryClaimAsync</c> on a <c>Failed</c> row
    /// resets it to <c>Pending</c> and re-claims, allowing a fresh send attempt.
    /// <para>
    /// Without this path, replaying a dead-lettered notification envelope via the DLQ admin
    /// endpoints would be a silent no-op — the handler would see the <c>Failed</c> row, skip
    /// the claim, and return without sending.
    /// </para>
    /// </summary>
    [Fact]
    public async Task TryClaimAsync_OnFailedRow_ResetsToPendingAndClaims_SupportingDlqReplay()
    {
        // Arrange — insert a Pending log, claim it, then mark it Failed (simulates terminal SMTP error).
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var guard = scope.ServiceProvider.GetRequiredService<NotificationSendGuard>();

        var idempotencyKey = Guid.NewGuid();
        var log = NotificationLog.Create(
            Guid.NewGuid(), "dlq-replay@example.com", NotificationType.WelcomeEmail,
            "Subject", clock.UtcNow, idempotencyKey);
        db.NotificationLogs.Add(log);
        await db.SaveChangesAsync();

        var firstToken = await guard.TryClaimAsync(idempotencyKey, CancellationToken.None);
        Assert.NotNull(firstToken);
        await guard.MarkFailedAsync(idempotencyKey, firstToken.Value, CancellationToken.None);

        var afterFailed = await db.NotificationLogs
            .AsNoTracking()
            .SingleAsync(l => l.IdempotencyKey == idempotencyKey);
        Assert.Equal(NotificationDeliveryStatus.Failed, afterFailed.DeliveryStatus);

        // Act — TryClaimAsync on a Failed row (simulates DLQ replay after root-cause fix).
        var replayToken = await guard.TryClaimAsync(idempotencyKey, CancellationToken.None);

        // Assert — guard recovers the Failed row and issues a fresh claim.
        Assert.NotNull(replayToken);
        var afterReplay = await db.NotificationLogs
            .AsNoTracking()
            .SingleAsync(l => l.IdempotencyKey == idempotencyKey);
        Assert.Equal(NotificationDeliveryStatus.Sending, afterReplay.DeliveryStatus);
        Assert.NotNull(afterReplay.SendingLeaseToken);
        Assert.Equal(replayToken.Value, afterReplay.SendingLeaseToken.Value);
    }
}

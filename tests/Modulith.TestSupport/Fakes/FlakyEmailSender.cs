using Modulith.Shared.Infrastructure.Notifications;

namespace Modulith.TestSupport.Fakes;

/// <summary>
/// Test double for <see cref="IEmailSender"/> that simulates a transient network failure on the
/// first call and succeeds on all subsequent calls. Designed for Wolverine retry-policy tests.
/// </summary>
/// <remarks>
/// <para>
/// Pass <c>clockAdvance = TimeSpan.Zero</c> to test the primary transient-recovery path where
/// <c>NotificationSendGuard.MarkReadyAsync</c> resets the Sending row to Pending immediately in
/// the handler's <c>catch (IOException)</c> block, enabling the Wolverine retry to re-claim
/// without any clock manipulation.
/// </para>
/// <para>
/// Pass a <paramref name="clockAdvance"/> that exceeds <c>NotificationSendGuard.StuckSendingThreshold</c>
/// (5 minutes) to exercise the <em>stale-row crash-recovery path</em> in <c>TryClaimAsync</c>.
/// The same <see cref="TestClock"/> instance must be registered as
/// <see cref="Modulith.Shared.Kernel.Interfaces.IClock"/> in the test host so the guard observes
/// the advanced time.
/// </para>
/// </remarks>
public sealed class FlakyEmailSender(TestClock clock, TimeSpan clockAdvance) : IEmailSender
{
    private readonly List<EmailMessage> _sent = [];
    private int _attempts;

    public IReadOnlyList<EmailMessage> SentMessages => _sent.AsReadOnly();

    public int TotalAttempts => _attempts;

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var attempt = Interlocked.Increment(ref _attempts);
        if (attempt == 1)
        {
            // Advance the clock BEFORE throwing so that when Wolverine retries the handler
            // the NotificationSendGuard's stale-row recovery path activates.
            clock.Advance(clockAdvance);
            throw new IOException("Simulated transient SMTP connection failure");
        }

        _sent.Add(message);
        return Task.CompletedTask;
    }
}

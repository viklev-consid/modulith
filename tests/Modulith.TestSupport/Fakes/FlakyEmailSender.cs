using Modulith.Shared.Infrastructure.Notifications;

namespace Modulith.TestSupport.Fakes;

/// <summary>
/// Test double for <see cref="IEmailSender"/> that simulates a failure on the first call and
/// succeeds on all subsequent calls. Designed for Wolverine retry-policy tests.
/// </summary>
/// <remarks>
/// <para>
/// Pass <paramref name="throwRetryable"/> as <c>true</c> (with <c>clockAdvance = TimeSpan.Zero</c>)
/// to test the primary transient-recovery path. The handler's <c>catch (RetryableSmtpException)</c>
/// block calls <c>NotificationSendGuard.MarkReadyAsync</c>, which resets the Sending row to Pending
/// immediately, allowing the Wolverine retry to re-claim without any clock manipulation.
/// </para>
/// <para>
/// Pass <paramref name="throwRetryable"/> as <c>false</c> (default) with a
/// <paramref name="clockAdvance"/> exceeding <c>NotificationSendGuard.StuckSendingThreshold</c>
/// (5 minutes) to exercise the <em>stale-row crash-recovery path</em> in <c>TryClaimAsync</c>.
/// The <see cref="IOException"/> bypasses the handler's catch blocks (simulating a crash with no
/// state cleanup), the row stays <c>Sending</c>, and the clock advance makes it eligible for
/// stale-row reset on the next retry. The same <see cref="TestClock"/> instance must be registered
/// as <see cref="Modulith.Shared.Kernel.Interfaces.IClock"/> in the test host.
/// </para>
/// </remarks>
public sealed class FlakyEmailSender(TestClock clock, TimeSpan clockAdvance, bool throwRetryable = false) : IEmailSender
{
    private readonly List<EmailMessage> sent = [];
    private int attempts;

    public IReadOnlyList<EmailMessage> SentMessages => sent.AsReadOnly();

    public int TotalAttempts => attempts;

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var attempt = Interlocked.Increment(ref attempts);
        if (attempt == 1)
        {
            // Advance the clock BEFORE throwing so that when Wolverine retries the handler
            // the NotificationSendGuard's stale-row recovery path can activate (if needed).
            clock.Advance(clockAdvance);

            if (throwRetryable)
            {
                // RetryableSmtpException is caught by the handler, which calls MarkReadyAsync
                // to reset the row to Pending immediately — no clock advance needed.
                throw new RetryableSmtpException("Simulated transient SMTP failure");
            }

            // Raw IOException bypasses the handler's catch blocks, simulating a process crash
            // where no state cleanup runs. Stale-row recovery in TryClaimAsync handles reset.
            throw new IOException("Simulated transient SMTP connection failure");
        }

        sent.Add(message);
        return Task.CompletedTask;
    }
}

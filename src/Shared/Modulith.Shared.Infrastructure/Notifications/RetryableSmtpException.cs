namespace Modulith.Shared.Infrastructure.Notifications;

/// <summary>
/// Thrown by <see cref="SmtpEmailSender"/> when a transient SMTP failure occurs that is
/// safe to retry: I/O errors, protocol errors, connection drops, authentication loss, and
/// 4xx server responses.
/// <para>
/// Handlers should catch this, call <c>NotificationSendGuard.MarkReadyAsync</c> to reset
/// the send claim to Pending, then rethrow so the Wolverine retry can re-claim immediately.
/// </para>
/// </summary>
public sealed class RetryableSmtpException : Exception
{
    public RetryableSmtpException() { }
    public RetryableSmtpException(string message) : base(message) { }
    public RetryableSmtpException(string message, Exception inner) : base(message, inner) { }
}

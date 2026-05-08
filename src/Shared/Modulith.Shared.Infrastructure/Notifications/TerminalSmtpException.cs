namespace Modulith.Shared.Infrastructure.Notifications;

/// <summary>
/// Thrown by <see cref="SmtpEmailSender"/> when the SMTP server returns a permanent error
/// (5xx status code). Retrying will not succeed; callers should mark the notification log
/// as <c>Failed</c> and let the message move to the dead-letter queue.
/// </summary>
public sealed class TerminalSmtpException : Exception
{
    public TerminalSmtpException() { }
    public TerminalSmtpException(string message) : base(message) { }
    public TerminalSmtpException(string message, Exception inner) : base(message, inner) { }
}

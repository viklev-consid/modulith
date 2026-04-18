namespace Modulith.Shared.Infrastructure.Notifications;

public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null,
    string? From = null);

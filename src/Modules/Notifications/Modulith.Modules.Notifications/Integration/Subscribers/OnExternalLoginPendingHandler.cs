using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Templates;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Infrastructure.Frontend;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;

using Wolverine.Attributes;

namespace Modulith.Modules.Notifications.Integration.Subscribers;

[NonTransactional]
public sealed class OnExternalLoginPendingHandler(
    NotificationsDbContext db,
    IEmailSender emailSender,
    IClock clock,
    NotificationSendGuard sendGuard,
    IFrontendUrlBuilder frontendUrls)
{
    public async Task Handle(ExternalLoginPendingV1 @event, CancellationToken ct)
    {
        using var activity = NotificationsTelemetry.ActivitySource.StartActivity(nameof(OnExternalLoginPendingHandler));
        NotificationsTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(ExternalLoginPendingV1)));

        var notificationType = @event.IsExistingUser
            ? NotificationType.ExternalLoginPendingExistingUser
            : NotificationType.ExternalLoginPendingNewUser;

        var subject = @event.IsExistingUser
            ? ExternalLoginPendingExistingUserTemplate.Subject
            : ExternalLoginPendingNewUserTemplate.Subject;

        var log = NotificationLog.Create(
            Guid.Empty, @event.Email, notificationType, subject, clock.UtcNow, @event.EventId);
        db.NotificationLogs.Add(log);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            db.Entry(log).State = EntityState.Detached;
        }

        if (await sendGuard.TryClaimAsync(@event.EventId, ct) is not { } leaseToken)
        {
            return;
        }

        var confirmationUrl = frontendUrls.ConfirmGoogleLogin(@event.RawToken);
        var (htmlBody, plainBody) = @event.IsExistingUser
            ? (ExternalLoginPendingExistingUserTemplate.HtmlBody(@event.RawToken, confirmationUrl),
               ExternalLoginPendingExistingUserTemplate.PlainTextBody(@event.RawToken, confirmationUrl))
            : (ExternalLoginPendingNewUserTemplate.HtmlBody(@event.RawToken, confirmationUrl),
               ExternalLoginPendingNewUserTemplate.PlainTextBody(@event.RawToken, confirmationUrl));

        var message = new EmailMessage(
            To: @event.Email,
            Subject: subject,
            HtmlBody: htmlBody,
            PlainTextBody: plainBody);

        try
        {
            await emailSender.SendAsync(message, ct);
        }
        catch (RetryableSmtpException)
        {
            await sendGuard.MarkReadyAsync(@event.EventId, leaseToken, ct);
            throw;
        }
        catch (TerminalSmtpException)
        {
            await sendGuard.MarkFailedAsync(@event.EventId, leaseToken, ct);
            throw;
        }
        await sendGuard.MarkSentAsync(@event.EventId, leaseToken, ct);
    }
}

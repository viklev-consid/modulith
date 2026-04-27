using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Templates;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Integration.Subscribers;

public sealed class OnExternalLoginPendingHandler(
    NotificationsDbContext db,
    IEmailSender emailSender,
    IClock clock)
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
            log = await db.NotificationLogs
                .FirstAsync(l => l.IdempotencyKey == @event.EventId, ct);
            if (log.DeliveryStatus == NotificationDeliveryStatus.Sent)
            {
                return;
            }
        }

        var (htmlBody, plainBody) = @event.IsExistingUser
            ? (ExternalLoginPendingExistingUserTemplate.HtmlBody(@event.RawToken),
               ExternalLoginPendingExistingUserTemplate.PlainTextBody(@event.RawToken))
            : (ExternalLoginPendingNewUserTemplate.HtmlBody(@event.RawToken),
               ExternalLoginPendingNewUserTemplate.PlainTextBody(@event.RawToken));

        var message = new EmailMessage(
            To: @event.Email,
            Subject: subject,
            HtmlBody: htmlBody,
            PlainTextBody: plainBody);

        await emailSender.SendAsync(message, ct);
        log.MarkSent();
        await db.SaveChangesAsync(ct);
    }
}

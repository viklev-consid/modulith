using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Templates;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Integration.Subscribers;

/// <summary>
/// Alerts the OLD email address after a confirmed email change — defence-in-depth
/// against silent account takeover.
/// </summary>
public sealed class OnEmailChangedHandler(
    NotificationsDbContext db,
    IEmailSender emailSender,
    IClock clock)
{
    public async Task Handle(EmailChangedV1 @event, CancellationToken ct)
    {
        using var activity = NotificationsTelemetry.ActivitySource.StartActivity(nameof(OnEmailChangedHandler));
        NotificationsTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(EmailChangedV1)));

        // Send to the OLD email — that is the address that needs the alert.
        var log = NotificationLog.Create(
            @event.UserId, @event.OldEmail, NotificationType.EmailChanged,
            EmailChangedTemplate.Subject, clock.UtcNow, @event.EventId);
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

        var message = new EmailMessage(
            To: @event.OldEmail,
            Subject: EmailChangedTemplate.Subject,
            HtmlBody: EmailChangedTemplate.HtmlBody(@event.NewEmail),
            PlainTextBody: EmailChangedTemplate.PlainTextBody(@event.NewEmail));

        await emailSender.SendAsync(message, ct);
        log.MarkSent();
        await db.SaveChangesAsync(ct);
    }
}

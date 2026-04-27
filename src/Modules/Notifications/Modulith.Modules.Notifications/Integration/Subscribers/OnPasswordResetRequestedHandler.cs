using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Templates;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Integration.Subscribers;

public sealed class OnPasswordResetRequestedHandler(
    NotificationsDbContext db,
    IEmailSender emailSender,
    IClock clock)
{
    public async Task Handle(PasswordResetRequestedV1 @event, CancellationToken ct)
    {
        using var activity = NotificationsTelemetry.ActivitySource.StartActivity(nameof(OnPasswordResetRequestedHandler));
        NotificationsTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(PasswordResetRequestedV1)));

        var log = NotificationLog.Create(
            @event.UserId, @event.Email, NotificationType.PasswordResetRequest,
            PasswordResetRequestTemplate.Subject, clock.UtcNow, @event.EventId);
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

        // The raw token is passed through as-is; the consuming client constructs the
        // full reset URL from its own base URL and this token.
        var message = new EmailMessage(
            To: @event.Email,
            Subject: PasswordResetRequestTemplate.Subject,
            HtmlBody: PasswordResetRequestTemplate.HtmlBody(@event.RawToken),
            PlainTextBody: PasswordResetRequestTemplate.PlainTextBody(@event.RawToken));

        await emailSender.SendAsync(message, ct);
        log.MarkSent();
        await db.SaveChangesAsync(ct);
    }
}

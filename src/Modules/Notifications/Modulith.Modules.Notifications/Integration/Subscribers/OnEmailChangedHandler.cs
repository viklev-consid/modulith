using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Templates;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Infrastructure.Notifications;
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
        var alreadySent = await db.NotificationLogs.AnyAsync(
            l => l.UserId == @event.UserId && l.NotificationType == NotificationType.EmailChanged,
            ct);

        if (alreadySent)
            return;

        // Send to the OLD email — that is the address that needs the alert.
        var message = new EmailMessage(
            To: @event.OldEmail,
            Subject: EmailChangedTemplate.Subject,
            HtmlBody: EmailChangedTemplate.HtmlBody(@event.NewEmail),
            PlainTextBody: EmailChangedTemplate.PlainTextBody(@event.NewEmail));

        await emailSender.SendAsync(message, ct);

        db.NotificationLogs.Add(NotificationLog.Create(
            @event.UserId, @event.OldEmail, NotificationType.EmailChanged,
            EmailChangedTemplate.Subject, clock.UtcNow));

        await db.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Templates;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Integration.Subscribers;

public sealed class OnEmailChangeRequestedHandler(
    NotificationsDbContext db,
    IEmailSender emailSender,
    IClock clock)
{
    public async Task Handle(EmailChangeRequestedV1 @event, CancellationToken ct)
    {
        var alreadySent = await db.NotificationLogs.AnyAsync(
            l => l.UserId == @event.UserId && l.NotificationType == NotificationType.EmailChangeRequest,
            ct);

        if (alreadySent)
        {
            return;
        }

        var message = new EmailMessage(
            To: @event.NewEmail,
            Subject: EmailChangeRequestTemplate.Subject,
            HtmlBody: EmailChangeRequestTemplate.HtmlBody(@event.RawToken),
            PlainTextBody: EmailChangeRequestTemplate.PlainTextBody(@event.RawToken));

        await emailSender.SendAsync(message, ct);

        db.NotificationLogs.Add(NotificationLog.Create(
            @event.UserId, @event.NewEmail, NotificationType.EmailChangeRequest,
            EmailChangeRequestTemplate.Subject, clock.UtcNow));

        await db.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Templates;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Integration.Subscribers;

public sealed class OnPasswordResetHandler(
    NotificationsDbContext db,
    IEmailSender emailSender,
    IClock clock)
{
    public async Task Handle(PasswordResetV1 @event, CancellationToken ct)
    {
        var alreadySent = await db.NotificationLogs.AnyAsync(
            l => l.UserId == @event.UserId && l.NotificationType == NotificationType.PasswordResetConfirmation,
            ct);

        if (alreadySent)
        {
            return;
        }

        var message = new EmailMessage(
            To: @event.Email,
            Subject: PasswordResetConfirmationTemplate.Subject,
            HtmlBody: PasswordResetConfirmationTemplate.HtmlBody(),
            PlainTextBody: PasswordResetConfirmationTemplate.PlainTextBody());

        await emailSender.SendAsync(message, ct);

        db.NotificationLogs.Add(NotificationLog.Create(
            @event.UserId, @event.Email, NotificationType.PasswordResetConfirmation,
            PasswordResetConfirmationTemplate.Subject, clock.UtcNow));

        await db.SaveChangesAsync(ct);
    }
}

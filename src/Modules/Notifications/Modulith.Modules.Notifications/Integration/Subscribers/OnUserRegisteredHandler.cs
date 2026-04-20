using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Consent;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Templates;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Integration.Subscribers;

public sealed class OnUserRegisteredHandler(
    NotificationsDbContext db,
    IEmailSender emailSender,
    IConsentRegistry consentRegistry,
    IClock clock)
{
    public async Task Handle(UserRegisteredV1 @event, CancellationToken ct)
    {
        if (!consentRegistry.HasConsented(@event.UserId, NotificationType.WelcomeEmail))
            return;

        // Idempotency guard — safe to re-run on Wolverine retries.
        var alreadySent = await db.NotificationLogs.AnyAsync(
            l => l.UserId == @event.UserId && l.NotificationType == NotificationType.WelcomeEmail,
            ct);

        if (alreadySent)
            return;

        var message = new EmailMessage(
            To: @event.Email,
            Subject: WelcomeEmailTemplate.Subject,
            HtmlBody: WelcomeEmailTemplate.HtmlBody(@event.DisplayName),
            PlainTextBody: WelcomeEmailTemplate.PlainTextBody(@event.DisplayName));

        await emailSender.SendAsync(message, ct);

        var log = NotificationLog.Create(
            @event.UserId,
            @event.Email,
            NotificationType.WelcomeEmail,
            WelcomeEmailTemplate.Subject,
            clock.UtcNow);

        db.NotificationLogs.Add(log);
        await db.SaveChangesAsync(ct);
    }
}

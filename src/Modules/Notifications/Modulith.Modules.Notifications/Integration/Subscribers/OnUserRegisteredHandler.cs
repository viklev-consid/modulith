using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Templates;
using Modulith.Modules.Users.Contracts;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;

using Wolverine.Attributes;

namespace Modulith.Modules.Notifications.Integration.Subscribers;

[NonTransactional]
public sealed class OnUserRegisteredHandler(
    NotificationsDbContext db,
    IEmailSender emailSender,
    IConsentRegistry consentRegistry,
    IClock clock,
    NotificationSendGuard sendGuard)
{
    public async Task Handle(UserRegisteredV1 @event, CancellationToken ct)
    {
        using var activity = NotificationsTelemetry.ActivitySource.StartActivity(nameof(OnUserRegisteredHandler));
        NotificationsTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(UserRegisteredV1)));

        if (!await consentRegistry.HasConsentedAsync(@event.UserId, ConsentKeys.WelcomeEmail, ct))
        {
            return;
        }

        var log = NotificationLog.Create(
            @event.UserId,
            @event.Email,
            NotificationType.WelcomeEmail,
            WelcomeEmailTemplate.Subject,
            clock.UtcNow,
            @event.EventId);
        db.NotificationLogs.Add(log);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            db.Entry(log).State = EntityState.Detached;
        }

        if (!await sendGuard.TryClaimAsync(@event.EventId, ct))
        {
            return;
        }

        var message = new EmailMessage(
            To: @event.Email,
            Subject: WelcomeEmailTemplate.Subject,
            HtmlBody: WelcomeEmailTemplate.HtmlBody(@event.DisplayName),
            PlainTextBody: WelcomeEmailTemplate.PlainTextBody(@event.DisplayName));

        await emailSender.SendAsync(message, ct);
        await sendGuard.MarkSentAsync(@event.EventId, ct);
    }
}

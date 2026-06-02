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
public sealed class OnUserEmailConfirmedHandler(
    NotificationsDbContext db,
    IEmailSender emailSender,
    IConsentRegistry consentRegistry,
    IClock clock,
    NotificationSendGuard sendGuard)
{
    public async Task Handle(UserEmailConfirmedV1 @event, CancellationToken ct)
    {
        using var activity = NotificationsTelemetry.ActivitySource.StartActivity(nameof(OnUserEmailConfirmedHandler));
        NotificationsTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(UserEmailConfirmedV1)));

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

        if (await sendGuard.TryClaimAsync(@event.EventId, ct) is not { } leaseToken)
        {
            return;
        }

        var message = new EmailMessage(
            To: @event.Email,
            Subject: WelcomeEmailTemplate.Subject,
            HtmlBody: WelcomeEmailTemplate.HtmlBody(@event.DisplayName),
            PlainTextBody: WelcomeEmailTemplate.PlainTextBody(@event.DisplayName));

        try
        {
            await sendGuard.SendWithLeaseRenewalAsync(
                @event.EventId, leaseToken,
                token => emailSender.SendAsync(message, token), ct);
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

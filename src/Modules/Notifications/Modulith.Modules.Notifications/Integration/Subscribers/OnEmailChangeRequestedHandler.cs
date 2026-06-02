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
public sealed class OnEmailChangeRequestedHandler(
    NotificationsDbContext db,
    IEmailSender emailSender,
    IClock clock,
    NotificationSendGuard sendGuard,
    IFrontendUrlBuilder frontendUrls)
{
    public async Task Handle(EmailChangeRequestedV1 @event, CancellationToken ct)
    {
        using var activity = NotificationsTelemetry.ActivitySource.StartActivity(nameof(OnEmailChangeRequestedHandler));
        NotificationsTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(EmailChangeRequestedV1)));

        var log = NotificationLog.Create(
            @event.UserId, @event.NewEmail, NotificationType.EmailChangeRequest,
            EmailChangeRequestTemplate.Subject, clock.UtcNow, @event.EventId);
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

        var confirmationUrl = frontendUrls.ConfirmEmailChange(@event.RawToken);
        var message = new EmailMessage(
            To: @event.NewEmail,
            Subject: EmailChangeRequestTemplate.Subject,
            HtmlBody: EmailChangeRequestTemplate.HtmlBody(@event.RawToken, confirmationUrl),
            PlainTextBody: EmailChangeRequestTemplate.PlainTextBody(@event.RawToken, confirmationUrl));

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

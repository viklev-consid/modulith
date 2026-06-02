using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Templates;
using Modulith.Modules.Organizations.Contracts.Events;
using Modulith.Shared.Infrastructure.Frontend;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine.Attributes;

namespace Modulith.Modules.Notifications.Integration.Subscribers;

[NonTransactional]
public sealed class OnOrganizationInvitationCreatedHandler(
    NotificationsDbContext db,
    IEmailSender emailSender,
    IClock clock,
    NotificationSendGuard sendGuard,
    IFrontendUrlBuilder frontendUrls)
{
    public async Task Handle(OrganizationInvitationCreatedV1 @event, CancellationToken ct)
    {
        using var activity = NotificationsTelemetry.ActivitySource.StartActivity(nameof(OnOrganizationInvitationCreatedHandler));
        NotificationsTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(OrganizationInvitationCreatedV1)));

        var log = NotificationLog.Create(
            Guid.Empty,
            @event.Email,
            NotificationType.OrganizationInvitation,
            OrganizationInvitationTemplate.Subject,
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

        var invitationUrl = frontendUrls.AcceptOrganizationInvitation(@event.RawToken, @event.Email);
        var message = new EmailMessage(
            To: @event.Email,
            Subject: OrganizationInvitationTemplate.Subject,
            HtmlBody: OrganizationInvitationTemplate.HtmlBody(@event.Role, @event.RawToken, invitationUrl),
            PlainTextBody: OrganizationInvitationTemplate.PlainTextBody(@event.Role, @event.RawToken, invitationUrl));

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

using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Templates;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Integration.Subscribers;

public sealed class OnExternalLoginUnlinkedHandler(
    NotificationsDbContext db,
    IEmailSender emailSender,
    IClock clock)
{
    public async Task Handle(ExternalLoginUnlinkedV1 @event, CancellationToken ct)
    {
        using var activity = NotificationsTelemetry.ActivitySource.StartActivity(nameof(OnExternalLoginUnlinkedHandler));
        NotificationsTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(ExternalLoginUnlinkedV1)));

        db.NotificationLogs.Add(NotificationLog.Create(
            @event.UserId, @event.Email, NotificationType.ExternalLoginUnlinked,
            ExternalLoginUnlinkedTemplate.Subject, clock.UtcNow, @event.EventId));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            return;
        }

        var message = new EmailMessage(
            To: @event.Email,
            Subject: ExternalLoginUnlinkedTemplate.Subject,
            HtmlBody: ExternalLoginUnlinkedTemplate.HtmlBody(@event.Provider),
            PlainTextBody: ExternalLoginUnlinkedTemplate.PlainTextBody(@event.Provider));

        await emailSender.SendAsync(message, ct);
    }
}

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

        db.NotificationLogs.Add(NotificationLog.Create(
            @event.UserId, @event.Email, NotificationType.PasswordResetRequest,
            PasswordResetRequestTemplate.Subject, clock.UtcNow, @event.EventId));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            return;
        }

        // The raw token is passed through as-is; the consuming client constructs the
        // full reset URL from its own base URL and this token.
        var message = new EmailMessage(
            To: @event.Email,
            Subject: PasswordResetRequestTemplate.Subject,
            HtmlBody: PasswordResetRequestTemplate.HtmlBody(@event.RawToken),
            PlainTextBody: PasswordResetRequestTemplate.PlainTextBody(@event.RawToken));

        await emailSender.SendAsync(message, ct);
    }
}

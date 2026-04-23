using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Templates;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Integration.Subscribers;

public sealed class OnPasswordResetHandler(
    NotificationsDbContext db,
    IEmailSender emailSender,
    IClock clock)
{
    public async Task Handle(PasswordResetV1 @event, CancellationToken ct)
    {
        using var activity = NotificationsTelemetry.ActivitySource.StartActivity(nameof(OnPasswordResetHandler));
        NotificationsTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(PasswordResetV1)));

        db.NotificationLogs.Add(NotificationLog.Create(
            @event.UserId, @event.Email, NotificationType.PasswordResetConfirmation,
            PasswordResetConfirmationTemplate.Subject, clock.UtcNow, @event.EventId));

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
            Subject: PasswordResetConfirmationTemplate.Subject,
            HtmlBody: PasswordResetConfirmationTemplate.HtmlBody,
            PlainTextBody: PasswordResetConfirmationTemplate.PlainTextBody);

        await emailSender.SendAsync(message, ct);
    }
}

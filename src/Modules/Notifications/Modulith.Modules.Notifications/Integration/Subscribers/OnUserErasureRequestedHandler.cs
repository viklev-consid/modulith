using Modulith.Modules.Notifications.Gdpr;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Kernel.Gdpr;
using Wolverine.Attributes;

namespace Modulith.Modules.Notifications.Integration.Subscribers;

[NonTransactional]
public sealed class OnUserErasureRequestedHandler(NotificationsPersonalDataEraser eraser)
{
    public async Task Handle(UserErasureRequestedV1 @event, CancellationToken ct)
    {
        using var activity = NotificationsTelemetry.ActivitySource.StartActivity(nameof(OnUserErasureRequestedHandler));
        NotificationsTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(UserErasureRequestedV1)));

        var userRef = new UserRef(@event.UserId, @event.DisplayName);
        await eraser.EraseAsync(userRef, ErasureStrategy.HardDelete, ct);
    }
}

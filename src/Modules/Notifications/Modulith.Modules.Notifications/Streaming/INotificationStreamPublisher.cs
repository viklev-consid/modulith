using ErrorOr;

namespace Modulith.Modules.Notifications.Streaming;

public interface INotificationStreamPublisher
{
    ErrorOr<Success> Subscribe(Guid userId, string clientId, ChannelWriterRegistration registration);

    ValueTask PublishAsync(Guid userId, NotificationStreamEvent streamEvent, CancellationToken ct);
}

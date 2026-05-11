namespace Modulith.Modules.Notifications.Streaming;

public interface INotificationStreamPublisher
{
    void Subscribe(Guid userId, ChannelWriterRegistration registration);

    ValueTask PublishAsync(Guid userId, NotificationStreamEvent streamEvent, CancellationToken ct);
}

using System.Threading.Channels;
using ErrorOr;
using Modulith.Modules.Notifications.Streaming;

namespace Modulith.Modules.Notifications.Features.StreamMyNotifications;

public sealed class StreamMyNotificationsHandler(INotificationStreamPublisher publisher)
{
    public Task<ErrorOr<StreamMyNotificationsResponse>> Handle(StreamMyNotificationsQuery query, CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<NotificationStreamEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        var registration = new ChannelWriterRegistration(channel.Writer);
        publisher.Subscribe(query.UserId, registration);

        return Task.FromResult<ErrorOr<StreamMyNotificationsResponse>>(new StreamMyNotificationsResponse(
            channel.Reader,
            registration));
    }
}

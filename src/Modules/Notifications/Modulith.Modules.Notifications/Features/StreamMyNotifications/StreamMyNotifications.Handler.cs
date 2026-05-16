using System.Threading.Channels;
using ErrorOr;
using Microsoft.Extensions.Options;
using Modulith.Modules.Notifications.Streaming;

namespace Modulith.Modules.Notifications.Features.StreamMyNotifications;

public sealed class StreamMyNotificationsHandler(
    INotificationStreamPublisher publisher,
    IOptions<NotificationsOptions> options)
{
    public Task<ErrorOr<StreamMyNotificationsResponse>> Handle(StreamMyNotificationsQuery query, CancellationToken ct)
    {
        var channel = Channel.CreateBounded<NotificationStreamEvent>(new BoundedChannelOptions(options.Value.Stream.ChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        var registration = new ChannelWriterRegistration(channel.Writer);
        var subscription = publisher.Subscribe(query.UserId, query.ClientId, registration);

        if (subscription.IsError)
        {
            return Task.FromResult<ErrorOr<StreamMyNotificationsResponse>>(subscription.Errors);
        }

        return Task.FromResult<ErrorOr<StreamMyNotificationsResponse>>(new StreamMyNotificationsResponse(
            channel.Reader,
            registration));
    }
}

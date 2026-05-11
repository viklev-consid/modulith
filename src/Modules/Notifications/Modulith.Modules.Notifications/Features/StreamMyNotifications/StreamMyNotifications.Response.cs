using System.Threading.Channels;
using Modulith.Modules.Notifications.Streaming;

namespace Modulith.Modules.Notifications.Features.StreamMyNotifications;

public sealed record StreamMyNotificationsResponse(
    ChannelReader<NotificationStreamEvent> Reader,
    IDisposable Subscription);

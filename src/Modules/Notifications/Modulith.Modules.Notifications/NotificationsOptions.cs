using System.ComponentModel.DataAnnotations;

namespace Modulith.Modules.Notifications;

public sealed class NotificationsOptions
{
    public RetentionOptions Retention { get; init; } = new();

    public StreamOptions Stream { get; init; } = new();

    public sealed class RetentionOptions
    {
        [Range(1, 3650)]
        public int DefaultUnreadDays { get; init; } = 90;

        [Range(1, 3650)]
        public int DefaultReadDays { get; init; } = 45;

        [Range(1, 3650)]
        public int DefaultArchivedDays { get; init; } = 7;

        [Range(1, 3650)]
        public int SecurityAndAccountDays { get; init; } = 365;
    }

    public sealed class StreamOptions
    {
        [Range(1, 10)]
        public int MaxActiveStreamsPerUser { get; init; } = 3;

        [Range(1, 1000)]
        public int ChannelCapacity { get; init; } = 100;
    }
}

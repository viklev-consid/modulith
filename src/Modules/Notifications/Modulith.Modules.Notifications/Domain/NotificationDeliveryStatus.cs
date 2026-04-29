namespace Modulith.Modules.Notifications.Domain;

public enum NotificationDeliveryStatus
{
    Pending = 0,
    Sent = 1,
    /// <summary>
    /// Exclusive send claim held by one handler instance. Transitions to <see cref="Sent"/>
    /// on success. Rows stuck in this state for longer than the configured threshold are
    /// eligible for automatic recovery — see <c>NotificationSendGuard</c>.
    /// </summary>
    Sending = 2
}

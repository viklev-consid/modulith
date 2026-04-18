namespace Modulith.Shared.Infrastructure.Notifications;

public interface ISmsSender
{
    Task SendAsync(SmsMessage message, CancellationToken ct);
}

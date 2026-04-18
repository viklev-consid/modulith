using Microsoft.Extensions.Logging;

namespace Modulith.Shared.Infrastructure.Notifications;

public sealed partial class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : ISmsSender
{
    public Task SendAsync(SmsMessage message, CancellationToken ct)
    {
        LogSms(logger, message.To, message.Body);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS to {To}: {Body}")]
    private static partial void LogSms(ILogger logger, string to, string body);
}

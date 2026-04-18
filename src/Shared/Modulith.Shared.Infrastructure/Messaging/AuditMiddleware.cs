using Microsoft.Extensions.Logging;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Shared.Infrastructure.Messaging;

public sealed partial class AuditMiddleware(ICurrentUser currentUser, ILogger<AuditMiddleware> logger)
{
    public Task AfterAsync(Envelope envelope, CancellationToken cancellationToken)
    {
        if (envelope.Message is not { } message) return Task.CompletedTask;

        LogMessageHandled(logger, message.GetType().Name, currentUser.Id ?? "system");
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Message {MessageType} handled by {ActorId}")]
    private static partial void LogMessageHandled(ILogger logger, string messageType, string actorId);
}

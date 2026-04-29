using Microsoft.Extensions.Logging;

namespace Modulith.Api.Infrastructure.Logging;

internal static partial class WolverineStartupLog
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Wolverine durable outbox is active (PostgreSQL transport, schema: wolverine).")]
    internal static partial void OutboxActive(ILogger logger);
}

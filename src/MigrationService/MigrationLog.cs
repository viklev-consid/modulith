using Microsoft.Extensions.Logging;

namespace Modulith.MigrationService;

internal static partial class MigrationLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Applying migrations for {DbContext}.")]
    public static partial void Applying(ILogger logger, string dbContext);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Database migrations completed.")]
    public static partial void Completed(ILogger logger);
}

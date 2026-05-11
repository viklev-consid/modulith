using TickerQ.Utilities.Base;
using Wolverine;

namespace Modulith.Modules.Users.Jobs;

public sealed class SweepExpiredTokensJob(IMessageBus bus)
{
    public const string Name = "users.sweep-expired-tokens";
    public const string CronExpression = "0 0 3 * * *";

    [TickerFunction(Name, CronExpression)]
    public async Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct)
    {
        await bus.InvokeAsync(new SweepExpiredTokens(), ct);
    }
}

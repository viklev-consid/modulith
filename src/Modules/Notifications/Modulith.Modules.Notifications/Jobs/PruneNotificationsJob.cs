using TickerQ.Utilities.Base;
using Wolverine;

namespace Modulith.Modules.Notifications.Jobs;

public sealed class PruneNotificationsJob(IMessageBus bus)
{
    public const string Name = "notifications.prune";
    public const string CronExpression = "0 0 4 * * *";

    [TickerFunction(Name, CronExpression)]
    public async Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct)
    {
        await bus.InvokeAsync(new PruneNotifications(), ct);
    }
}

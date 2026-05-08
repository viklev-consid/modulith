using Modulith.Modules.ModuleName.Contracts.Events;

namespace Modulith.Modules.ModuleName.Integration.Subscribers;

public sealed class OnEventNameHandler
{
    public async Task Handle(EventNameV1 @event, CancellationToken ct)
    {
        using var activity = ModuleNameTelemetry.ActivitySource.StartActivity(nameof(OnEventNameHandler));
        ModuleNameTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(EventNameV1)));

        _ = (@event, ct);
        await Task.CompletedTask;
    }
}

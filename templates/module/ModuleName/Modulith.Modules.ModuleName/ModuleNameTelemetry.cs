using System.Diagnostics;
using System.Diagnostics.Metrics;
using ErrorOr;

namespace Modulith.Modules.ModuleName;

internal static class ModuleNameTelemetry
{
    internal const string SourceName = "Modulith.Modules.ModuleName";
    internal const string MeterName = "Modulith.Modules.ModuleName";

    internal static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");

    private static readonly Meter meter = new(MeterName, "1.0.0");

    internal static readonly Counter<long> CommandsHandled =
        meter.CreateCounter<long>(
            "modulith.modulenamelower.commands.handled",
            description: "Total commands successfully handled by the ModuleName module.");

    internal static readonly Counter<long> CommandsFailed =
        meter.CreateCounter<long>(
            "modulith.modulenamelower.commands.failed",
            description: "Total command failures in the ModuleName module.");

    internal static readonly Counter<long> EventsPublished =
        meter.CreateCounter<long>(
            "modulith.modulenamelower.events.published",
            description: "Total integration events published by the ModuleName module.");

    internal static readonly Counter<long> EventsProcessed =
        meter.CreateCounter<long>(
            "modulith.modulenamelower.events.processed",
            description: "Total integration events processed by the ModuleName module.");

    internal static async Task<ErrorOr<T>> InstrumentAsync<T>(
        string operation,
        Func<Task<ErrorOr<T>>> handler)
    {
        using var activity = ActivitySource.StartActivity(operation);
        try
        {
            var result = await handler();
            if (result.IsError)
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.FirstError.Description);
                CommandsFailed.Add(1, new KeyValuePair<string, object?>("operation", operation));
            }
            else
            {
                CommandsHandled.Add(1, new KeyValuePair<string, object?>("operation", operation));
            }

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            CommandsFailed.Add(1, new KeyValuePair<string, object?>("operation", operation));
            throw;
        }
    }
}

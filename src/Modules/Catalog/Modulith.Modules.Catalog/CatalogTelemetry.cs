using System.Diagnostics;
using System.Diagnostics.Metrics;
using ErrorOr;

namespace Modulith.Modules.Catalog;

/// <summary>
/// OpenTelemetry instrumentation primitives for the Catalog module.
/// ActivitySource for distributed tracing; Meter/counters for metrics.
/// </summary>
internal static class CatalogTelemetry
{
    internal const string SourceName = "Modulith.Modules.Catalog";
    internal const string MeterName = "Modulith.Modules.Catalog";

    internal static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    internal static readonly Counter<long> CommandsHandled =
        Meter.CreateCounter<long>(
            "modulith.catalog.commands.handled",
            description: "Total commands successfully handled by the Catalog module.");

    internal static readonly Counter<long> CommandsFailed =
        Meter.CreateCounter<long>(
            "modulith.catalog.commands.failed",
            description: "Total command failures in the Catalog module.");

    internal static readonly Counter<long> EventsPublished =
        Meter.CreateCounter<long>(
            "modulith.catalog.events.published",
            description: "Total integration events published by the Catalog module.");

    internal static readonly Counter<long> EventsProcessed =
        Meter.CreateCounter<long>(
            "modulith.catalog.events.processed",
            description: "Total integration events processed by the Catalog module.");

    /// <summary>
    /// Wraps an <see cref="ErrorOr{T}"/> handler with an Activity span and success/failure counters.
    /// </summary>
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

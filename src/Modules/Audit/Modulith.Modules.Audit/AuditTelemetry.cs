using System.Diagnostics;
using System.Diagnostics.Metrics;
using ErrorOr;

namespace Modulith.Modules.Audit;

/// <summary>
/// OpenTelemetry instrumentation primitives for the Audit module.
/// ActivitySource for distributed tracing; Meter/counters for metrics.
/// </summary>
internal static class AuditTelemetry
{
    internal const string SourceName = "Modulith.Modules.Audit";
    internal const string MeterName = "Modulith.Modules.Audit";

    internal static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    internal static readonly Counter<long> QueriesHandled =
        Meter.CreateCounter<long>(
            "modulith.audit.queries.handled",
            description: "Total queries successfully handled by the Audit module.");

    internal static readonly Counter<long> QueriesFailed =
        Meter.CreateCounter<long>(
            "modulith.audit.queries.failed",
            description: "Total query failures in the Audit module.");

    internal static readonly Counter<long> EventsProcessed =
        Meter.CreateCounter<long>(
            "modulith.audit.events.processed",
            description: "Total integration events processed by the Audit module.");

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
                QueriesFailed.Add(1, new KeyValuePair<string, object?>("operation", operation));
            }
            else
            {
                QueriesHandled.Add(1, new KeyValuePair<string, object?>("operation", operation));
            }

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            QueriesFailed.Add(1, new KeyValuePair<string, object?>("operation", operation));
            throw;
        }
    }
}

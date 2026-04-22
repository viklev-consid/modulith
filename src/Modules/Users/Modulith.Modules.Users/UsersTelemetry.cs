using System.Diagnostics;
using System.Diagnostics.Metrics;
using ErrorOr;

namespace Modulith.Modules.Users;

/// <summary>
/// OpenTelemetry instrumentation primitives for the Users module.
/// ActivitySource for distributed tracing; Meter/counters for metrics.
/// </summary>
internal static class UsersTelemetry
{
    internal const string SourceName = "Modulith.Modules.Users";
    internal const string MeterName = "Modulith.Modules.Users";

    internal static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    internal static readonly Counter<long> CommandsHandled =
        Meter.CreateCounter<long>(
            "modulith.users.commands.handled",
            description: "Total commands successfully handled by the Users module.");

    internal static readonly Counter<long> CommandsFailed =
        Meter.CreateCounter<long>(
            "modulith.users.commands.failed",
            description: "Total command failures in the Users module.");

    internal static readonly Counter<long> EventsPublished =
        Meter.CreateCounter<long>(
            "modulith.users.events.published",
            description: "Total integration events published by the Users module.");

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

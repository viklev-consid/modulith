using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Modulith.Modules.Notifications;

/// <summary>
/// OpenTelemetry instrumentation primitives for the Notifications module.
/// ActivitySource for distributed tracing; Meter/counters for metrics.
/// </summary>
internal static class NotificationsTelemetry
{
    internal const string SourceName = "Modulith.Modules.Notifications";
    internal const string MeterName = "Modulith.Modules.Notifications";

    internal static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    internal static readonly Counter<long> EventsProcessed =
        Meter.CreateCounter<long>(
            "modulith.notifications.events.processed",
            description: "Total integration events processed by the Notifications module.");
}

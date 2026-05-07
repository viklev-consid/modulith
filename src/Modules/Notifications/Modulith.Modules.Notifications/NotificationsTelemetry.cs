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

    internal static readonly Counter<long> EmailsSent =
        Meter.CreateCounter<long>(
            "modulith.notifications.emails.sent",
            description: "Emails delivered successfully (Sending → Sent).");

    internal static readonly Counter<long> EmailsFailedTransient =
        Meter.CreateCounter<long>(
            "modulith.notifications.emails.failed.transient",
            description: "Emails that failed with a retryable SMTP error (Sending → Pending reset).");

    internal static readonly Counter<long> EmailsFailedTerminal =
        Meter.CreateCounter<long>(
            "modulith.notifications.emails.failed.terminal",
            description: "Emails that failed permanently (Sending → Failed).");

    internal static readonly Counter<long> SendGuardClaims =
        Meter.CreateCounter<long>(
            "modulith.notifications.send_guard.claims",
            description: "Successful exclusive send-claim acquisitions (Pending → Sending).");

    internal static readonly Counter<long> SendGuardStaleRecoveries =
        Meter.CreateCounter<long>(
            "modulith.notifications.send_guard.stale_recoveries",
            description: "Stale Sending rows reset to Pending by the crash-recovery path in TryClaimAsync.");

    internal static readonly Counter<long> SendGuardFailedRecoveries =
        Meter.CreateCounter<long>(
            "modulith.notifications.send_guard.failed_recoveries",
            description: "Failed rows reset to Pending for DLQ replay by TryClaimAsync.");
}

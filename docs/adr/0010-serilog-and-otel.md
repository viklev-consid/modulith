# ADR-0010: Serilog Routed Through OpenTelemetry

## Status

Accepted

## Context

Modern observability requires logs, metrics, and traces to correlate — a log line from a handler should be linkable to the trace of the containing request. .NET Aspire's `ServiceDefaults` provides an OpenTelemetry pipeline out of the box that sends all three to the Aspire dashboard (and, in production, to any OTLP-compatible backend).

`Microsoft.Extensions.Logging` is the default log abstraction and it routes to OTel cleanly. But it is underpowered for structured logging: the message-template story is good, enrichers are minimal, sink flexibility is weak, and destructuring policies for masking sensitive properties are absent.

**Serilog** provides:

- A mature structured logging model
- Rich enricher ecosystem (`FromLogContext`, `WithMachineName`, `WithSpan` for OTel correlation)
- Destructuring policies for masking sensitive values
- File/console/OTLP sinks
- Configuration-driven setup (`ReadFrom.Configuration`) that ops teams can adjust without code changes

The tension: if Serilog replaces the default MEL-to-OTel pipeline naively, log entries stop appearing in the Aspire dashboard because they no longer reach the OTel provider.

## Decision

1. **Use Serilog** as the log pipeline, configured via `appsettings.json` using `ReadFrom.Configuration`.
2. **A bootstrap logger** is configured in `Program.cs` (before the host is built) using minimal settings, to capture startup errors.
3. **Serilog is wired to write to OTLP** via `Serilog.Sinks.OpenTelemetry`, so logs reach the Aspire dashboard and production OTel collectors with trace correlation intact.
4. **Standard enrichers**: `FromLogContext`, `WithMachineName`, `WithEnvironmentName`, `WithSpan` (for OTel trace/span ID correlation), `WithExceptionDetails`.
5. **A destructuring policy masks sensitive properties** — any property marked `[PersonalData]`, `[SensitivePersonalData]`, or matching a name pattern (`password`, `token`, `secret`) is replaced with `***`.
6. **Console sink for dev** (readable format), **OTLP sink for prod**. File sinks are not shipped; teams who want them add their own.

## Consequences

**Positive:**

- Logs appear in the Aspire dashboard with trace correlation.
- Config-driven: ops can change log levels per namespace without deploying.
- Sensitive properties are masked by default — aligned with ADR-0012 (GDPR Primitives).
- `ILogger<T>` still works throughout the app — Serilog plugs in as the provider.

**Negative:**

- An extra library (Serilog + sinks) over the built-in MEL.
- Two logger lifecycles (bootstrap and host) — developers need to understand why.
- The destructuring policy must be kept in sync with the personal-data attributes (ADR-0012). Tested in `Modulith.Architecture.Tests`.

## Configuration

An example `appsettings.json` shape:

```json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.OpenTelemetry" ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "OpenTelemetry",
        "Args": {
          "Endpoint": "http://localhost:4317",
          "Protocol": "Grpc"
        }
      }
    ],
    "Enrich": [ "FromLogContext", "WithMachineName", "WithEnvironmentName", "WithSpan" ]
  }
}
```

## Related

- ADR-0012 (GDPR Primitives): the `[PersonalData]` / `[SensitivePersonalData]` attributes that drive masking.
- ADR-0021 (Config and Secrets): logging config is part of the standard hierarchy.

# ADR-0011: Two-Layer Auditing — Row-Level Fields and Dedicated Audit Module

## Status

Accepted

## Context

"Auditing" means two distinct things, and conflating them produces bad designs:

1. **Row-level audit metadata** — who created this row, when, who last updated it. Answers "who touched this?" for any given record.
2. **Change history / audit trail** — the sequence of semantic events that occurred over time. Answers "what happened to this entity, in order, and why?"

Row-level metadata is cheap, lives on the entity itself, and is useful for 80% of audit questions. Change history is expensive, requires separate storage, and is essential for compliance scenarios (regulatory, user-facing "activity log", forensic investigations).

Implementations often conflate them, either by putting change history on the entity (bloats the row, hard to query) or by omitting row-level fields (every question requires a change-history query).

For the Users module specifically, change history is not optional — login history, role changes, and email changes are typically required by compliance and by "account activity" user-facing views.

## Decision

Two independent layers:

### Layer 1: Row-level audit fields

`Shared.Kernel` defines `IAuditableEntity`:

```csharp
public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; }
    string? CreatedBy { get; }
    DateTimeOffset? UpdatedAt { get; }
    string? UpdatedBy { get; }
}
```

A `SaveChangesInterceptor` in `Shared.Infrastructure` populates these fields on insert/update by reading `ICurrentUser`. This is deliberately a `SaveChangesInterceptor`, not a generic `IDbCommandInterceptor` — only the former has access to the change tracker and entity types.

`ICurrentUser` is the abstraction for "who is acting right now." Its default implementation reads from `HttpContext`; in background jobs it falls back to a configured system identity (e.g., `"system:outbox"` or `"system:scheduler"`).

### Layer 2: Dedicated Audit module

A module (`Modulith.Modules.Audit`) that:

- Subscribes to integration events from other modules via Wolverine
- Persists a normalized change log (`AuditEntry` with `EventId`, `OccurredAt`, `Actor`, `EntityType`, `EntityId`, `Action`, `Payload`)
- Has its own schema (`audit`)
- Exposes a query API for change history (`GetAuditTrail(EntityRef)`)

Modules that want their changes audited publish integration events. The Audit module consumes them. No module is forced to participate.

The Users module explicitly participates (compliance requirement): login events, role changes, email changes, password resets all emit audit-worthy integration events.

## Consequences

**Positive:**

- Cheap questions are cheap. "Who last touched this order?" reads two columns on the order row.
- Expensive questions are possible. "Show me everything that happened to user X in the last 90 days" queries the audit module.
- Audit is GDPR-friendly. The audit module's data is separate; erasure flows can decide whether to erase audit entries or anonymize the actor (typically anonymize — the audit trail is a legitimate-interest basis).
- Modules opt in. Catalog probably doesn't need a change log; it doesn't pay the cost.
- Audit retention is independent. Raw audit may be kept for 1 year; operational data has its own retention.

**Negative:**

- Two mechanisms to understand. Documentation and naming keep them distinct.
- Audit entries are eventually consistent with the source-of-truth data (outbox delay).
- The audit module is a single point of interest for compliance officers — its availability matters.
- Custom queries for audit trail require knowing the schema. Abstracted behind a query API.

## Related

- ADR-0003 (Wolverine): the outbox that delivers integration events to the audit module.
- ADR-0005 (Module Communication): audit subscribes via public contracts.
- ADR-0012 (GDPR Primitives): erasure and export consider audit data.

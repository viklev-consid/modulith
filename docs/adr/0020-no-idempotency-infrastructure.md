# ADR-0020: No Built-in Idempotency Infrastructure

## Status

Accepted

## Context

Idempotency is a concern in any system with retries — HTTP clients retrying on timeout, message buses redelivering on failure, background jobs rescheduling after a crash. The patterns are well-known:

1. **HTTP-level**: `Idempotency-Key` header, server-side cache of `(key, response)` for some TTL.
2. **Message-handler level**: `processed_messages` table keyed by message ID, inserted inside the handler's transaction.
3. **Business-key dedup**: dedup on a logical key when the message ID differs but the operation is the same.

The question isn't *whether* idempotency matters — it does — but whether the **template** should bake it in, and the answer depends on use case. A payments API needs aggressive idempotency. An internal CRUD API for 50 users does not.

Arguments for baking it in:
- "Most apps eventually need it."
- "Safer defaults."

Arguments against:
- Extra storage, extra complexity.
- Implementation choices (TTL, key scope, storage backend) are opinionated in ways that don't fit every use case.
- Teams that don't need it have to understand the mechanism to remove it.
- It occupies mental budget that could go toward domain concerns.

The fundamental principle: **a template should make the right things obvious and the wrong things hard, not pre-solve every distributed systems problem.**

## Decision

**Do not bake idempotency infrastructure into the template.** Instead:

1. **Document the concern** in `ARCHITECTURE.md` and the Wolverine-related ADRs.
2. **Document the patterns** — HTTP-level `Idempotency-Key` middleware, handler-level processed-messages table, business-key dedup. Reference code sketches without shipping implementations.
3. **Establish "natural idempotency" as a convention.** Handlers should be designed to tolerate re-execution where feasible. State-based operations (`SetStatus(Shipped)`) are preferred over delta-based (`IncrementCounter()`). This is a code-review discipline, not framework infrastructure.
4. **Note that Wolverine's outbox provides at-least-once producer delivery.** Consumers should assume they may see the same message twice and design accordingly.

When a team needs idempotency:
- Adding HTTP `Idempotency-Key` middleware is ~150 lines.
- Adding a `processed_messages` table per module is a migration + a Wolverine middleware (~50 lines).
- Both fit cleanly into the existing architecture without retrofitting.

## Consequences

**Positive:**

- Template stays lean. Users don't pay costs they don't need.
- Idempotency choices remain the team's decision, with full context.
- No false sense of safety from baked-in infra that doesn't fit the use case.
- Smaller surface area to understand on first read.

**Negative:**

- Teams that need idempotency from day one have to add it themselves. Documented, but still work.
- Risk of teams shipping production with insufficient idempotency because it wasn't obvious to add. Mitigated by `ARCHITECTURE.md` prominence and by code-review discipline.
- No canonical implementation in the codebase to copy. Makes the "right way" slightly harder to find.

## Extension pattern

Documented in `how-to/add-idempotency.md`:

### HTTP-level

```csharp
// IdempotencyMiddleware sketch
// Key: Idempotency-Key header + authenticated user ID
// Store: (key, request_fingerprint, response) with 24h TTL in Redis or HybridCache
// On retry same key + same fingerprint: return cached response
// On retry same key + different fingerprint: 422 — client reused key with different payload
```

### Handler-level

```csharp
// Module adds: processed_messages table (MessageId PK, ProcessedAt)
// Wolverine middleware: INSERT (inside handler txn), PK violation → skip handler
// Works because Wolverine already opens a txn per handler
```

## Related

- ADR-0003 (Wolverine): the outbox that makes this concern visible.
- ADR-0005 (Module Communication): message-based communication implies possible redelivery.

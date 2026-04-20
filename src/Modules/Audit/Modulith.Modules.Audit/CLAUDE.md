# CLAUDE.md — Audit Module

This module records a tamper-evident audit trail of significant domain events across the system. It subscribes to integration events from other modules and persists them as immutable `AuditEntry` records.

---

## Domain vocabulary

- **AuditEntry** — an immutable record of something that happened. Identified by `AuditEntryId` (typed Guid).
- **EventType** — a dot-separated string naming the event (e.g. `user.registered`, `user.email_changed`).
- **ActorId** — the user who caused the event (nullable; system events may have no actor).
- **ResourceType** — the kind of entity affected (e.g. `User`, `Product`).
- **ResourceId** — the specific entity affected.
- **Payload** — a JSONB snapshot of the integration event at the time of recording.

---

## Invariants

1. `AuditEntry` records are never updated or deleted — the table is append-only by convention.
2. All new event types must be added to `Integration/Subscribers/`.
3. `GetAuditTrailQuery` is in `Audit.Contracts` so other modules can invoke the audit trail.
4. The endpoint returns the calling user's own trail. Admin access across users is a future concern.

---

## Adding a new audit subscriber

1. Create `On<EventName>Handler.cs` in `Integration/Subscribers/`.
2. Handle the contract event, call `AuditEntry.Create(...)`, save.
3. Register the handler in `AuditModule.AddAuditHandlers`.

---

## Known footguns

- `AuditEntry` has a parameterless private constructor for EF hydration. Do not call it; use `AuditEntry.Create(...)`.
- The `Payload` column is `jsonb` — Postgres can index and query into it. Avoid storing sensitive personal data there; if needed, store a reference key instead.

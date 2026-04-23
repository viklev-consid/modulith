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

1. `AuditEntry` records are never updated or deleted — the table is append-only by convention. The sole exception is GDPR erasure, which anonymizes (does not delete) matching rows.
2. All new event types must be added to `Integration/Subscribers/`.
3. `GetAuditTrailQuery` is in `Audit.Contracts` so other modules can invoke the audit trail.
4. `GetAuditTrail` is authenticated; the `AuditTrailPolicy` enforces ownership. Regular users see
   only their own trail (`actorId` defaults to caller's ID). Admins with `audit.trail.read` may
   pass an explicit `actorId` to query any user's trail.

---

## Payload policy

Payloads must contain only non-personal identifiers — `UserId` and event-specific metadata that carries no PII. Never store email addresses, display names, IP addresses, or other personal data in the payload. The `EventType`, `OccurredAt`, `ActorId`, and `ResourceId` columns provide full context without PII.

Exception: `user.role_changed` stores `OldRole`, `NewRole`, and `ChangedBy` — role names and admin IDs are not personal data.

---

## GDPR erasure behaviour

`AuditPersonalDataEraser` anonymizes all entries where the user is either the **actor** (`ActorId == userId`) or the **resource** (`ResourceId == userId`). For each matched row it:
- Nulls `ActorId` if it equals the user's ID
- Nulls `ResourceId` if it equals the user's ID
- Redacts any payload keys matching `email`, `mail`, `ipAddress`, or `displayName` (belt-and-suspenders for legacy rows)

`AuditPersonalDataExporter` exports the same set of rows (actor or resource) for GDPR data portability. The two must remain in sync — if you change the exporter query, change the eraser query to match.

---

## Adding a new audit subscriber

1. Create `On<EventName>Handler.cs` in `Integration/Subscribers/`.
2. Handle the contract event, call `AuditEntry.Create(...)`, save.
3. Register the handler in `AuditModule.AddAuditHandlers`.

---

## Known footguns

- `AuditEntry` has a parameterless private constructor for EF hydration. Do not call it; use `AuditEntry.Create(...)`.
- **Never put PII in payloads.** The policy is enforced by convention, not by code — a subscriber that does `JsonSerializer.Serialize(@event)` on an event carrying email, display name, or IP will silently persist that PII. Always project only `{UserId}` (plus non-personal metadata where useful). See the Payload policy section above.
- **Eraser covers both actor and resource rows.** If you add a subscriber where a user appears as `resourceId` but not `actorId` (e.g., admin actions on the user), erasure already handles it. You do not need to do anything special.
- **Keep exporter and eraser queries in sync.** Both filter on `ActorId == userId || ResourceId == userId`. If you change one, change the other.

# AGENTS.md - Audit Module

This module records a tamper-evident audit trail of significant domain events across the system. It subscribes to integration events from other modules and persists immutable `AuditEntry` records.

For general module conventions, see [`../../AGENTS.md`](../../AGENTS.md).

---

## Domain vocabulary

- **AuditEntry** - an immutable record of something that happened. Identified by `AuditEntryId` (typed Guid).
- **EventType** - a dot-separated string naming the event, such as `user.registered`.
- **ActorId** - the user who caused the event. Nullable for system events.
- **ResourceType** - the kind of entity affected, such as `User` or `Product`.
- **ResourceId** - the specific entity affected.
- **Payload** - a JSONB snapshot of non-personal event metadata at the time of recording.

---

## Invariants

1. `AuditEntry` records are never updated or deleted. The sole exception is GDPR erasure, which anonymizes matching rows.
2. All new event types must be added to `Integration/Subscribers/`.
3. `GetAuditTrailQuery` is in `Audit.Contracts` so other modules can invoke the audit trail.
4. `GetAuditTrail` is authenticated. `AuditTrailPolicy` enforces ownership: regular users see only their own trail, while admins with `audit.trail.read` may query any user's trail.

---

## Payload policy

Payloads must contain only non-personal identifiers: `UserId` and event-specific metadata that carries no PII. Never store email addresses, display names, IP addresses, or other personal data in the payload. The `EventType`, `OccurredAt`, `ActorId`, and `ResourceId` columns provide context without PII.

Exception: `user.role_changed` stores `OldRole`, `NewRole`, and `ChangedBy`; role names and admin IDs are not personal data.

---

## GDPR erasure behaviour

`AuditPersonalDataEraser` anonymizes all entries where the user is either the actor (`ActorId == userId`) or the resource (`ResourceId == userId`). For each matched row it:

- Nulls `ActorId` if it equals the user's ID.
- Nulls `ResourceId` if it equals the user's ID.
- Redacts payload keys matching `email`, `mail`, `ipAddress`, or `displayName`.

`AuditPersonalDataExporter` exports the same row set. Keep exporter and eraser queries in sync.

---

## Adding a new audit subscriber

1. Create `On<EventName>Handler.cs` in `Integration/Subscribers/`.
2. Handle the contract event, call `AuditEntry.Create(...)`, and save.
3. Register the handler in `AuditModule.AddAuditHandlers`.

---

## Known footguns

- `AuditEntry` has a parameterless private constructor for EF hydration. Do not call it; use `AuditEntry.Create(...)`.
- Never put PII in payloads. Do not serialize whole events that carry email, display name, or IP. Always project only non-personal fields.
- Erasure covers both actor and resource rows. If you add a subscriber where a user appears as `resourceId` but not `actorId`, erasure already handles it.
- Keep exporter and eraser queries in sync. Both filter on `ActorId == userId || ResourceId == userId`.

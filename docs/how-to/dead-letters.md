# Dead-Letter Queue Operations

This runbook covers the full operator lifecycle for dead-lettered messages in Modulith: inspection, replay, removal, and retention policy.

---

## Background

When a Wolverine message handler exhausts its retry budget, or when an explicit `MoveToErrorQueue` error policy fires (see `Program.cs`), the envelope is written to `wolverine.wolverine_dead_letters` inside the PostgreSQL `wolverine` schema. Nothing is lost — the original message body, exception details, and source queue are all preserved.

---

## Retention policy

Dead letters are **retained for 30 days** from arrival. Wolverine's built-in background job purges expired rows automatically; no cron job or manual cleanup is needed. The configuration is in `src/Api/Program.cs`:

```csharp
opts.Durability.DeadLetterQueueExpirationEnabled = true;
opts.Durability.DeadLetterQueueExpiration = TimeSpan.FromDays(30);
```

If 30 days is insufficient for your SLA, raise a PR that updates the constant and adds an ADR entry. If you want to delete individual messages earlier, use the discard endpoint below.

---

## HTTP endpoints (preferred operator path)

Five endpoints are mounted at `/v1/admin/dead-letters`. They require the **Admin authorization policy** — a JWT with `role = "admin"`. Obtain a token via the standard login flow as an admin-role user.

All endpoints are documented in the OpenAPI spec (Scalar UI at `/scalar/v1` in development).

### List dead letters

```
GET /v1/admin/dead-letters?pageNumber=1&pageSize=50&messageType=...&exceptionType=...
```

Returns a `DeadLetterEnvelopeResults` object with `TotalCount`, `PageNumber`, and `Envelopes[]`. Filter by `messageType` (full CLR name) or `exceptionType` (full CLR name) to narrow results.

**Example:**

```bash
curl -H "Authorization: Bearer $ADMIN_TOKEN" \
  "https://api.example.com/v1/admin/dead-letters?exceptionType=System.InvalidOperationException"
```

### Get a single dead letter

```
GET /v1/admin/dead-letters/{id}
```

Returns the `DeadLetterEnvelope` for the given envelope GUID. The envelope includes `ExceptionType`, `ExceptionMessage`, `MessageType`, `SentAt`, and `Replayable`.

### Replay dead letters

```
POST /v1/admin/dead-letters/replay
Content-Type: application/json

{
  "messageIds": ["<guid1>", "<guid2>"]
}
```

Re-enqueues the matched messages into the local durable queue for reprocessing. Wolverine sets them as `Replayable` and the scheduler picks them up. Returns `202 Accepted`.

You can also filter by type instead of listing IDs:

```json
{
  "messageType": "Modulith.Modules.Notifications.SendWelcomeEmail",
  "exceptionType": "System.IO.IOException"
}
```

> **Before replaying:** ensure the underlying issue that caused the failure is fixed. Replaying into a broken handler will re-dead-letter the message.

### Discard dead letters

```
POST /v1/admin/dead-letters/discard
Content-Type: application/json

{
  "messageIds": ["<guid1>"]
}
```

Permanently deletes the matched messages from `wolverine_dead_letters`. Use this for messages that are stale, superseded, or should never be reprocessed (e.g., events for users who have been erased). Returns `204 No Content`.

---

## Authorization

All five endpoints require `role = "admin"` in the bearer token. The `Admin` authorization policy is defined in `src/Api/Program.cs`. Normal `user`-role tokens receive `403 Forbidden`. Unauthenticated requests receive `401 Unauthorized`.

To obtain an admin token in development:

1. Register a user via `POST /v1/users/register`.
2. Promote the user to admin via `PUT /v1/users/{userId}/role` (requires an existing admin token or the seeded admin account).
3. Log in via `POST /v1/users/login` and use the returned JWT.

---

## SQL path (read-only inspection or emergency access)

If the HTTP surface is unavailable (e.g., during an outage), inspect the table directly:

```sql
-- Count by exception type
SELECT exception_type, COUNT(*) AS cnt
FROM wolverine.wolverine_dead_letters
GROUP BY exception_type
ORDER BY cnt DESC;

-- Inspect recent failures
SELECT id, message_type, exception_type, exception_message, received_at
FROM wolverine.wolverine_dead_letters
ORDER BY received_at DESC
LIMIT 50;

-- Mark as replayable by exception type (Wolverine scheduler will re-enqueue)
UPDATE wolverine.wolverine_dead_letters
SET replayable = true
WHERE exception_type = 'System.IO.IOException';
```

Do not delete rows directly unless the HTTP endpoints are unavailable — use the discard endpoint so the operation is auditable.

---

## Alerting recommendations

The DLQ is only useful if someone is watching it. Set up at least these two alerts:

**Alert 1 — DLQ is growing (any type)**

```sql
SELECT COUNT(*) FROM wolverine.wolverine_dead_letters;
```

Trigger when count exceeds your SLA threshold (e.g., > 0 for critical paths, > 10 for bulk paths). The OpenTelemetry metrics emitted by the Notifications module (`modulith.notifications.emails.failed.terminal`, `modulith.notifications.send_guard.failed_recoveries`) can also drive this alert from your metrics platform without a direct DB query.

**Alert 2 — Notification failure rate spike**

Monitor `modulith.notifications.emails.failed.transient` vs `modulith.notifications.emails.sent`. A sustained ratio above ~10 % over a 5-minute window indicates SMTP degradation and the retry queue is filling up.

**Alert 3 — DLQ approaching retention limit**

Messages older than 25 days are approaching the 30-day automatic purge window. If an incident is still unresolved at that point, messages will be deleted permanently before they can be replayed.

```sql
SELECT COUNT(*), MAX(received_at)
FROM wolverine.wolverine_dead_letters
WHERE received_at < NOW() - INTERVAL '25 days';
```

---

## Operational checklist after an incident

1. **List** dead letters via `GET /v1/admin/dead-letters` to understand the scope.
2. **Inspect** individual messages via `GET /v1/admin/dead-letters/{id}` to confirm the failure reason.
3. **Fix the root cause** (deploy a code fix, restore a downstream service, correct bad data).
4. **Replay** the affected messages via `POST /v1/admin/dead-letters/replay` with an appropriate filter.
5. **Monitor** — confirm the replayed messages process cleanly (check dead-letter count returns to zero for that type).
6. **Discard** any messages that should not be replayed (data already superseded, erased users, etc.).

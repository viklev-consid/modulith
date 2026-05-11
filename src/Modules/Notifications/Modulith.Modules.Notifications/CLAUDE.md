# CLAUDE.md — Notifications Module

This module owns user notifications. It has two intentionally separate surfaces:

- **Email notifications** for account/security/lifecycle communication such as password reset, password changed, email change, welcome, and external-login events.
- **Bell notifications** for product activity shown in-app, such as replies, mentions, assignments, follows, approvals, and workflow updates. SSE is only a live transport for bell updates; it is not a notification channel.

Do not automatically mirror email notifications into bell notifications. Account/security notifications are email-only unless a product decision explicitly says otherwise.

---

## Domain vocabulary

- **NotificationLog** — a record that a notification was attempted or sent. Identified by `NotificationLogId`. Carries a `DeliveryStatus` and a `SendingLeaseToken` for exclusive-claim validation.
- **NotificationType** — an enum classifying the notification. See the type definition for current values.
- **NotificationDeliveryStatus** — `Pending = 0` (row written, not yet claimed) / `Sending = 2` (exclusive claim held, SMTP in progress) / `Sent = 1` (delivery confirmed) / `Failed = 3` (terminal SMTP failure; row moved to DLQ).
- **NotificationSendGuard** — scoped service in `Integration/Subscribers/` that owns the atomic claim/recovery logic. Inject it into every notification handler.
- **IConsentRegistry** — interface (in `Shared.Infrastructure`) implemented by the Users module. Returns whether a user has consented to a given notification purpose.
- **UserNotification** — persisted in-app/bell notification visible to one recipient.
- **NotificationPreference** — per-user category settings for `Bell` and `Email`. Locked account/security defaults are not user-editable.
- **NotificationRetentionPolicy** — calculates when bell notifications become eligible for pruning.

---

## Invariants

1. Every handler is idempotent. Each `NotificationLog` row carries a unique `IdempotencyKey` (sourced from `@event.EventId`) backed by a DB-level unique constraint. On duplicate delivery the insert throws `DbUpdateException`; the handler detaches the entity and falls through to the claim step.
2. `DeliveryStatus` uses a four-state protocol (`Pending → Sending → Sent` or `→ Failed`) to prevent duplicate sends. `RetryableSmtpException` resets to `Pending` via `MarkReadyAsync`. `TerminalSmtpException` transitions to `Failed` via `MarkFailedAsync`. Three recovery paths exist: (a) `MarkReadyAsync` for immediate transient retry, (b) stale-row reset after 5 minutes for crash recovery, (c) `Failed → Pending` reset for DLQ replay after root-cause fix.
3. `IConsentRegistry` gates every notification type. Security notifications (`PasswordReset*`, `PasswordChanged`, `EmailChange*`, `ExternalLogin*`) bypass consent because they are security-critical.
4. `PasswordResetRequestedV1`, `EmailChangeRequestedV1`, and `ExternalLoginPendingV1` carry a raw token — embed it in the email body link, never log it.
5. Bell notifications are idempotent by `IdempotencyKey`. If a duplicate insert fails, detach the added entity before returning the existing row.
6. Bell endpoints are current-user scoped under `/v1/me/...`; do not add `/users/{id}/notifications` without an explicit admin use case.
7. Retention is stored per bell notification in `RetentionUntil`; cleanup deletes rows whose retention has elapsed.

---

## Adding a new notification type

### Email notification

1. Add an entry to the `NotificationType` enum.
2. Add a template in `Templates/` (static class with `Subject`, `HtmlBody`, `PlainTextBody`).
3. Write a handler in `Integration/Subscribers/` subscribing to the triggering event. Copy from any existing handler (e.g. `OnUserRegisteredHandler.cs`).
4. Register the handler in `NotificationsModule.AddNotificationsHandlers`.

Key rules when writing the handler:
- **Always detach the failed entity** (`db.Entry(log).State = EntityState.Detached`) before the claim; otherwise Wolverine's `AutoApplyTransactions` middleware will try to re-insert the `Added` entity on any subsequent `SaveChangesAsync`.
- **Never call `log.MarkSent()` directly** — use `sendGuard.MarkSentAsync`. The guard issues an `ExecuteUpdateAsync` that bypasses EF change tracking, which is correct here because handlers are `[NonTransactional]`.
- **Both catch blocks are required.** Omitting `catch (RetryableSmtpException)` leaves the row stuck in `Sending` until the 5-minute stale threshold. Omitting `catch (TerminalSmtpException)` leaves it stuck permanently. Both must pass the `leaseToken` from `TryClaimAsync`.

### Bell notification

Prefer product modules publishing their own integration event and the Notifications module subscribing to it. For simple reusable cases, other modules may invoke `CreateNotificationCommand` from `Modulith.Modules.Notifications.Contracts`.

Bell notification rules:

- Use `NotificationCategory.Product`, `Collaboration`, or `System` for product-facing activity.
- `Account` and `Security` default to email-only and should not become bell notifications casually.
- Store rendered `Title` and `Body`; historical bell items should remain stable even if source data changes later.
- Provide a stable `IdempotencyKey` from the source event id or workflow id.
- Use `NotificationChannel.Bell` only when the type really belongs in the in-app feed.

User-facing bell endpoints:

- `GET /v1/me/notifications`
- `GET /v1/me/notifications/unread-count`
- `PATCH /v1/me/notifications/{notificationId}/read`
- `PATCH /v1/me/notifications/read-all`
- `DELETE /v1/me/notifications/{notificationId}` archives/hides the row
- `GET /v1/me/notifications/stream` streams SSE events
- `GET|PUT /v1/me/notification-preferences`

---

## Email infrastructure

`IEmailSender` → `SmtpEmailSender` (from `Shared.Infrastructure`). Configured from `Modules:Notifications:Smtp` section. Defaults to localhost:1025 (Mailpit in dev).

---

## Known footguns

- `SmtpEmailSender` is a real SMTP client — integration tests must override `IEmailSender` with a fake to avoid SMTP dial failures.
- Raw tokens arrive in `PasswordResetRequestedV1.RawToken`, `EmailChangeRequestedV1.RawToken`, and `ExternalLoginPendingV1.RawToken`. Embed them in email body links; never log them. Serilog destructuring masks known token property names, but defense-in-depth means not calling the log statement at all.
- The unique constraint on `NotificationLog.IdempotencyKey` makes duplicate-detection race-safe — catch `DbUpdateException.IsUniqueConstraintViolation()` rather than doing a pre-check with `AnyAsync`. But do NOT short-circuit on the constraint alone; always fall through to `TryClaimAsync`.
- Adding a subscriber for a new event requires registering the handler in `NotificationsModule.AddNotificationsHandlers`; forgetting this means handlers are never discovered by Wolverine.
- Adding a scheduled cleanup handler requires registering it in `AddNotificationsHandlers` and anchoring the TickerQ job in `AddNotificationsJobs`.

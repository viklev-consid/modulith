# CLAUDE.md — Notifications Module

This module delivers transactional notifications to users and keeps a log of what was sent. It subscribes to integration events from other modules and sends email via `IEmailSender`.

---

## Domain vocabulary

- **NotificationLog** — a record that a notification was attempted or sent. Identified by `NotificationLogId`. Carries a `DeliveryStatus` and a `SendingLeaseToken` for exclusive-claim validation.
- **NotificationType** — an enum classifying the notification. See the type definition for current values.
- **NotificationDeliveryStatus** — `Pending = 0` (row written, not yet claimed) / `Sending = 2` (exclusive claim held, SMTP in progress) / `Sent = 1` (delivery confirmed) / `Failed = 3` (terminal SMTP failure; row moved to DLQ).
- **NotificationSendGuard** — scoped service in `Integration/Subscribers/` that owns the atomic claim/recovery logic. Inject it into every notification handler.
- **IConsentRegistry** — interface (in `Shared.Infrastructure`) implemented by the Users module. Returns whether a user has consented to a given notification purpose.

---

## Invariants

1. Every handler is idempotent. Each `NotificationLog` row carries a unique `IdempotencyKey` (sourced from `@event.EventId`) backed by a DB-level unique constraint. On duplicate delivery the insert throws `DbUpdateException`; the handler detaches the entity and falls through to the claim step.
2. `DeliveryStatus` uses a four-state protocol (`Pending → Sending → Sent` or `→ Failed`) to prevent duplicate sends. `RetryableSmtpException` resets to `Pending` via `MarkReadyAsync`. `TerminalSmtpException` transitions to `Failed` via `MarkFailedAsync`. Three recovery paths exist: (a) `MarkReadyAsync` for immediate transient retry, (b) stale-row reset after 5 minutes for crash recovery, (c) `Failed → Pending` reset for DLQ replay after root-cause fix.
3. `IConsentRegistry` gates every notification type. Security notifications (`PasswordReset*`, `PasswordChanged`, `EmailChange*`, `ExternalLogin*`) bypass consent because they are security-critical.
4. `PasswordResetRequestedV1`, `EmailChangeRequestedV1`, and `ExternalLoginPendingV1` carry a raw token — embed it in the email body link, never log it.

---

## Adding a new notification type

1. Add an entry to the `NotificationType` enum.
2. Add a template in `Templates/` (static class with `Subject`, `HtmlBody`, `PlainTextBody`).
3. Write a handler in `Integration/Subscribers/` subscribing to the triggering event. Copy from any existing handler (e.g. `OnUserRegisteredHandler.cs`).
4. Register the handler in `NotificationsModule.AddNotificationsHandlers`.

Key rules when writing the handler:
- **Always detach the failed entity** (`db.Entry(log).State = EntityState.Detached`) before the claim; otherwise Wolverine's `AutoApplyTransactions` middleware will try to re-insert the `Added` entity on any subsequent `SaveChangesAsync`.
- **Never call `log.MarkSent()` directly** — use `sendGuard.MarkSentAsync`. The guard issues an `ExecuteUpdateAsync` that bypasses EF change tracking, which is correct here because handlers are `[NonTransactional]`.
- **Both catch blocks are required.** Omitting `catch (RetryableSmtpException)` leaves the row stuck in `Sending` until the 5-minute stale threshold. Omitting `catch (TerminalSmtpException)` leaves it stuck permanently. Both must pass the `leaseToken` from `TryClaimAsync`.

---

## Email infrastructure

`IEmailSender` → `SmtpEmailSender` (from `Shared.Infrastructure`). Configured from `Modules:Notifications:Smtp` section. Defaults to localhost:1025 (Mailpit in dev).

---

## Known footguns

- `SmtpEmailSender` is a real SMTP client — integration tests must override `IEmailSender` with a fake to avoid SMTP dial failures.
- Raw tokens arrive in `PasswordResetRequestedV1.RawToken`, `EmailChangeRequestedV1.RawToken`, and `ExternalLoginPendingV1.RawToken`. Embed them in email body links; never log them. Serilog destructuring masks known token property names, but defense-in-depth means not calling the log statement at all.
- The unique constraint on `NotificationLog.IdempotencyKey` makes duplicate-detection race-safe — catch `DbUpdateException.IsUniqueConstraintViolation()` rather than doing a pre-check with `AnyAsync`. But do NOT short-circuit on the constraint alone; always fall through to `TryClaimAsync`.
- Adding a subscriber for a new event requires registering the handler in `NotificationsModule.AddNotificationsHandlers`; forgetting this means handlers are never discovered by Wolverine.

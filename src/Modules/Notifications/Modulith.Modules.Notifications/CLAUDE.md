# CLAUDE.md — Notifications Module

This module delivers transactional notifications to users and keeps a log of what was sent. It subscribes to integration events from other modules and sends email via `IEmailSender`.

---

## Domain vocabulary

- **NotificationLog** — an immutable record that a notification was sent. Identified by `NotificationLogId`.
- **NotificationType** — an enum classifying the notification (`WelcomeEmail`, `PasswordResetRequest`, `PasswordResetConfirmation`, `PasswordChanged`, `EmailChangeRequest`, `EmailChanged`).
- **IConsentRegistry** — interface (in `Shared.Infrastructure`) implemented by the Users module. Returns whether a user has consented to a given notification purpose.

---

## Notifications currently delivered

| Trigger event | Template | Recipient |
|---|---|---|
| `UserRegisteredV1` | Welcome email | Registered email address |
| `PasswordResetRequestedV1` | Password reset link | Registered email address |
| `PasswordResetV1` | Reset confirmation | Registered email address |
| `PasswordChangedV1` | Password changed alert | Registered email address |
| `EmailChangeRequestedV1` | Email change confirmation link | **New** email address |
| `EmailChangedV1` | Email changed alert | **Old** email address |

---

## Invariants

1. Every handler is idempotent — each `NotificationLog` row carries a unique `IdempotencyKey` (sourced from `@event.EventId`) backed by a DB-level unique constraint. The handler inserts the log row first; a `DbUpdateException` with a unique-constraint violation means the event was already processed and the handler returns early.
2. The log is written *before* the email is sent. Because Wolverine wraps the handler in a transaction, if `SendAsync` throws the transaction is rolled back, the log row is not persisted, and the retry will re-insert and re-send correctly. This prevents the double-send that the old send-before-log ordering could cause when `SaveChangesAsync` failed after a successful send.
3. `IConsentRegistry` gates every notification type. The welcome email checks `ConsentKeys.WelcomeEmail`. Security notifications (`PasswordReset*`, `PasswordChanged`, `EmailChange*`) are transactional — they bypass consent because they are security-critical.
4. `PasswordResetRequestedV1` and `EmailChangeRequestedV1` carry a raw token — embed it in the email body link, never log it.

---

## Adding a new notification type

1. Add an entry to the `NotificationType` enum.
2. Add a template in `Templates/` (static class with `Subject`, `HtmlBody`, `PlainTextBody`).
3. Write a handler in `Integration/Subscribers/` subscribing to the triggering event.
4. Include an idempotency guard: add the log row first (with `@event.EventId` as `idempotencyKey`), catch `DbUpdateException.IsUniqueConstraintViolation()` and return early, then send the email.
5. Register the handler in `NotificationsModule.AddNotificationsHandlers`.

---

## Email infrastructure

`IEmailSender` → `SmtpEmailSender` (from `Shared.Infrastructure`). Configured from `Modules:Notifications:Smtp` section. Defaults to localhost:1025 (Mailpit in dev).

---

## Known footguns

- `SmtpEmailSender` is a real SMTP client — integration tests must override `IEmailSender` with a fake to avoid SMTP dial failures.
- Raw tokens arrive in `PasswordResetRequestedV1.RawToken` and `EmailChangeRequestedV1.RawToken`. Embed them in email body links; never log them. Serilog destructuring masks known token property names, but defense-in-depth means not calling the log statement at all.
- The idempotency key is `@event.EventId` (set by the publishing handler in Users). All events in `Users.Contracts` carry this field. The unique constraint on `NotificationLog.IdempotencyKey` makes the dedup race-safe — catch `DbUpdateException.IsUniqueConstraintViolation()` rather than doing a pre-check with `AnyAsync`.
- Adding a subscriber for a new event requires registering the handler in `NotificationsModule.AddNotificationsHandlers`; forgetting this means handlers are never discovered by Wolverine.

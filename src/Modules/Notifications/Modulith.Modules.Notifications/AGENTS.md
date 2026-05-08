# AGENTS.md - Notifications Module

This module delivers transactional notifications to users and keeps a log of what was sent. It subscribes to integration events from other modules and sends email via `IEmailSender`.

For general module conventions, see [`../../AGENTS.md`](../../AGENTS.md).

---

## Domain vocabulary

- **NotificationLog** - a record that a notification was attempted or sent. Carries `DeliveryStatus` and a `SendingLeaseToken` for exclusive-claim validation.
- **NotificationType** - an enum classifying the notification.
- **NotificationDeliveryStatus** - `Pending = 0`, `Sending = 2`, `Sent = 1`, `Failed = 3`.
- **NotificationSendGuard** - scoped service in `Integration/Subscribers/` that owns atomic claim and recovery logic. Inject it into every notification handler.
- **IConsentRegistry** - interface in `Shared.Infrastructure`, implemented by Users. Returns whether a user consented to a notification purpose.

---

## Invariants

1. Every handler is idempotent. Each `NotificationLog` row carries a unique `IdempotencyKey` from `@event.EventId`, backed by a DB unique constraint.
2. `DeliveryStatus` uses a four-state protocol (`Pending -> Sending -> Sent` or `Pending -> Sending -> Failed`) to prevent duplicate sends.
3. `RetryableSmtpException` resets to `Pending` via `MarkReadyAsync`; `TerminalSmtpException` transitions to `Failed` via `MarkFailedAsync`.
4. `IConsentRegistry` gates every notification type. Security notifications bypass consent because they are security-critical.
5. Raw tokens from password reset, email change, and external-login events may be embedded in email links but must never be logged.

---

## Adding a new notification type

1. Add an entry to the `NotificationType` enum.
2. Add a template in `Templates/` with `Subject`, `HtmlBody`, and `PlainTextBody`.
3. Write a handler in `Integration/Subscribers/` subscribing to the triggering event.
4. Register the handler in `NotificationsModule.AddNotificationsHandlers`.

Key handler rules:

- Always detach the failed entity (`db.Entry(log).State = EntityState.Detached`) before the claim.
- Never call `log.MarkSent()` directly; use `sendGuard.MarkSentAsync`.
- Keep both `catch (RetryableSmtpException)` and `catch (TerminalSmtpException)` blocks, and pass the `leaseToken` from `TryClaimAsync`.

---

## Email infrastructure

`IEmailSender` maps to `SmtpEmailSender` from `Shared.Infrastructure`, configured from `Modules:Notifications:Smtp`. Defaults point at localhost:1025 (Mailpit in dev).

---

## Known footguns

- `SmtpEmailSender` is a real SMTP client. Integration tests must override `IEmailSender` with a fake.
- Raw tokens arrive in `PasswordResetRequestedV1.RawToken`, `EmailChangeRequestedV1.RawToken`, and `ExternalLoginPendingV1.RawToken`. Embed them in email body links; never log them.
- The `NotificationLog.IdempotencyKey` unique constraint makes duplicate detection race-safe. Catch `DbUpdateException.IsUniqueConstraintViolation()` rather than pre-checking with `AnyAsync`, then still fall through to `TryClaimAsync`.
- Adding a subscriber for a new event requires registering the handler in `NotificationsModule.AddNotificationsHandlers`.

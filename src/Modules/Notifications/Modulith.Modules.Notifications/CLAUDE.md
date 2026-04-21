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

1. Every handler is idempotent — it checks for an existing `NotificationLog` entry before sending to handle Wolverine retries.
2. Email is sent *before* the log is written. If send fails the handler will be retried by Wolverine. If log write fails after send, the idempotency guard prevents a double send.
3. `IConsentRegistry` gates every notification type. The welcome email checks `ConsentKeys.WelcomeEmail`. Security notifications (`PasswordReset*`, `PasswordChanged`, `EmailChange*`) are transactional — they bypass consent because they are security-critical.
4. `PasswordResetRequestedV1` and `EmailChangeRequestedV1` carry a raw token — embed it in the email body link, never log it.

---

## Adding a new notification type

1. Add an entry to the `NotificationType` enum.
2. Add a template in `Templates/` (static class with `Subject`, `HtmlBody`, `PlainTextBody`).
3. Write a handler in `Integration/Subscribers/` subscribing to the triggering event.
4. Include an idempotency guard (query `NotificationLogs` before sending).
5. Register the handler in `NotificationsModule.AddNotificationsHandlers`.

---

## Email infrastructure

`IEmailSender` → `SmtpEmailSender` (from `Shared.Infrastructure`). Configured from `Modules:Notifications:Smtp` section. Defaults to localhost:1025 (Mailpit in dev).

---

## Known footguns

- `SmtpEmailSender` is a real SMTP client — integration tests must override `IEmailSender` with a fake to avoid SMTP dial failures.
- Raw tokens arrive in `PasswordResetRequestedV1.RawToken` and `EmailChangeRequestedV1.RawToken`. Embed them in email body links; never log them. Serilog destructuring masks known token property names, but defense-in-depth means not calling the log statement at all.
- The `NotificationLog` idempotency query uses `(UserId, NotificationType)` as the key for one-per-user notifications. For flows that legitimately repeat (e.g., multiple password-reset requests), use the raw token or a per-event ID as the dedup key instead.
- Adding a subscriber for a new event requires registering the handler in `NotificationsModule.AddNotificationsHandlers`; forgetting this means handlers are never discovered by Wolverine.

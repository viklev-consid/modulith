# CLAUDE.md — Notifications Module

This module is responsible for delivering notifications to users and keeping a log of what was sent. In Phase 7 it supports welcome emails triggered by user registration.

---

## Domain vocabulary

- **NotificationLog** — an immutable record that a notification was sent. Identified by `NotificationLogId`.
- **NotificationType** — an enum classifying the notification (`WelcomeEmail`, etc.).

---

## Invariants

1. `OnUserRegisteredHandler` is idempotent — it checks for an existing `NotificationLog` entry before sending to handle Wolverine retries.
2. Email is sent *before* the log is written. If send fails the handler will be retried by Wolverine. If log write fails after send, the idempotency guard prevents a double send.
3. `IConsentRegistry` gates every notification type. The Phase 7 stub always grants consent; Phase 8 replaces it with a real implementation.

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

- `IConsentRegistry` is `internal`. Phase 8 will expose a real implementation. Do not make it public.
- `AlwaysGrantedConsentRegistry` is a stub — do not rely on it for production consent tracking.
- `SmtpEmailSender` is a real SMTP client — integration tests must override `IEmailSender` with a fake to avoid SMTP dial failures.

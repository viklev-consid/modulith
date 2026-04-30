# CLAUDE.md — Notifications Module

This module delivers transactional notifications to users and keeps a log of what was sent. It subscribes to integration events from other modules and sends email via `IEmailSender`.

---

## Domain vocabulary

- **NotificationLog** — a record that a notification was attempted or sent. Identified by `NotificationLogId`. Carries a `DeliveryStatus` (`Pending` / `Sending` / `Sent`) and a `SendingClaimedAt` timestamp that tracks the exclusive send claim.
- **NotificationType** — an enum classifying the notification (`WelcomeEmail`, `PasswordResetRequest`, `PasswordResetConfirmation`, `PasswordChanged`, `EmailChangeRequest`, `EmailChanged`, `ExternalLoginPendingExistingUser`, `ExternalLoginPendingNewUser`, `ExternalLoginLinked`, `ExternalLoginUnlinked`).
- **NotificationDeliveryStatus** — `Pending = 0` (row written, not yet claimed) / `Sending = 2` (exclusive claim held, SMTP in progress) / `Sent = 1` (delivery confirmed).
- **NotificationSendGuard** — scoped service in `Integration/Subscribers/` that owns the atomic claim/recovery logic. Inject it into every notification handler.
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
| `ExternalLoginPendingV1` | Pending confirmation link | Registered email address |
| `ExternalLoginLinkedV1` | Provider linked alert | Registered email address |
| `ExternalLoginUnlinkedV1` | Provider unlinked alert | Registered email address |

---

## Invariants

1. Every handler is idempotent. Each `NotificationLog` row carries a unique `IdempotencyKey` (sourced from `@event.EventId`) backed by a DB-level unique constraint. On a duplicate event delivery the insert throws `DbUpdateException` with a unique-constraint violation; the handler detaches the entity and falls through to the claim step.
2. `DeliveryStatus` uses a four-state protocol to prevent duplicate sends. The sequence is: insert log as `Pending` → atomically transition `Pending → Sending` (`NotificationSendGuard.TryClaimAsync`) → send email → transition `Sending → Sent` (`NotificationSendGuard.MarkSentAsync`). **If `emailSender.SendAsync` throws `RetryableSmtpException`**, the handler catches it, calls `NotificationSendGuard.MarkReadyAsync` to reset the row back to `Pending`, then rethrows — allowing the Wolverine retry to re-claim immediately. **If it throws `TerminalSmtpException`** (permanent 5xx error), the handler calls `NotificationSendGuard.MarkFailedAsync` to transition `Sending → Failed`, then rethrows — Wolverine moves the message to the dead-letter queue. If the process crashes between the claim and `MarkSentAsync`, the row stays `Sending`; `TryClaimAsync` will reset it to `Pending` and re-claim after `StuckSendingThreshold` (5 minutes) has elapsed (crash-recovery only).
3. `IConsentRegistry` gates every notification type. The welcome email checks `ConsentKeys.WelcomeEmail`. Security notifications (`PasswordReset*`, `PasswordChanged`, `EmailChange*`, `ExternalLogin*`) are transactional — they bypass consent because they are security-critical.
4. `PasswordResetRequestedV1`, `EmailChangeRequestedV1`, and `ExternalLoginPendingV1` carry a raw token — embed it in the email body link, never log it.

---

## Adding a new notification type

1. Add an entry to the `NotificationType` enum.
2. Add a template in `Templates/` (static class with `Subject`, `HtmlBody`, `PlainTextBody`).
3. Write a handler in `Integration/Subscribers/` subscribing to the triggering event. Follow the exact pattern below (copy from any existing handler).
4. Register the handler in `NotificationsModule.AddNotificationsHandlers`.

### Idempotency + atomic send-claim pattern

```csharp
// 1. Insert log as Pending; idempotency key unique constraint prevents duplicate rows.
var log = NotificationLog.Create(@event.UserId, @event.Email, NotificationType.Foo,
    FooTemplate.Subject, clock.UtcNow, @event.EventId);
db.NotificationLogs.Add(log);

try
{
    await db.SaveChangesAsync(ct);
}
catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
{
    // Row already exists from a prior attempt — detach and fall through to the claim.
    db.Entry(log).State = EntityState.Detached;
}

// 2. Atomically claim the send slot (Pending → Sending).
//    Returns false if the row is already Sending (another instance in-flight) or Sent.
//    Stale Sending rows (> 5 min) are automatically reset to Pending and re-claimed.
if (!await sendGuard.TryClaimAsync(@event.EventId, ct))
{
    return;
}

// 3. We hold the exclusive claim — send the email.
//    SmtpEmailSender classifies all MailKit exceptions:
//      - RetryableSmtpException: reset the claim to Pending so the Wolverine retry can
//        re-claim immediately without waiting for the 5-minute stale-row threshold.
//      - TerminalSmtpException: mark the row Failed and let Wolverine dead-letter it.
var message = new EmailMessage(To: @event.Email, Subject: FooTemplate.Subject,
    HtmlBody: FooTemplate.HtmlBody(...), PlainTextBody: FooTemplate.PlainTextBody(...));

try
{
    await emailSender.SendAsync(message, ct);
}
catch (RetryableSmtpException)
{
    await sendGuard.MarkReadyAsync(@event.EventId, ct);
    throw;
}
catch (TerminalSmtpException)
{
    await sendGuard.MarkFailedAsync(@event.EventId, ct);
    throw;
}

// 4. Confirm delivery (Sending → Sent).
await sendGuard.MarkSentAsync(@event.EventId, ct);
```

Key rules:
- **Always detach the failed entity** (`db.Entry(log).State = EntityState.Detached`) before the claim; otherwise Wolverine's `AutoApplyTransactions` middleware will try to re-insert the `Added` entity on any subsequent `SaveChangesAsync`.
- **Never call `log.MarkSent()` directly** — use `sendGuard.MarkSentAsync`. The guard issues an `ExecuteUpdateAsync` that bypasses EF change tracking, which is correct here because handlers are `[NonTransactional]`.
- **Inject `NotificationSendGuard sendGuard`** in the handler's primary constructor alongside `db` and `clock`.

---

## Email infrastructure

`IEmailSender` → `SmtpEmailSender` (from `Shared.Infrastructure`). Configured from `Modules:Notifications:Smtp` section. Defaults to localhost:1025 (Mailpit in dev).

---

## Known footguns

- `SmtpEmailSender` is a real SMTP client — integration tests must override `IEmailSender` with a fake to avoid SMTP dial failures.
- Raw tokens arrive in `PasswordResetRequestedV1.RawToken`, `EmailChangeRequestedV1.RawToken`, and `ExternalLoginPendingV1.RawToken`. Embed them in email body links; never log them. Serilog destructuring masks known token property names, but defense-in-depth means not calling the log statement at all.
- The unique constraint on `NotificationLog.IdempotencyKey` makes duplicate-detection race-safe — catch `DbUpdateException.IsUniqueConstraintViolation()` rather than doing a pre-check with `AnyAsync`. But do NOT short-circuit on the constraint alone; always fall through to `TryClaimAsync`.
- Adding a subscriber for a new event requires registering the handler in `NotificationsModule.AddNotificationsHandlers`; forgetting this means handlers are never discovered by Wolverine.
- Rows stuck in `Sending` due to a process crash are recovered automatically after 5 minutes by the `TryClaimAsync` stale-row path on the next Wolverine retry. This path is **crash recovery only** — transient SMTP failures are handled explicitly: `RetryableSmtpException` triggers `MarkReadyAsync` (resets to `Pending` immediately), and `TerminalSmtpException` triggers `MarkFailedAsync` (transitions to `Failed`, message dead-lettered). If you add a new notification handler, you must include both catch blocks — omitting `RetryableSmtpException` will leave the row stuck in `Sending` until the 5-minute threshold; omitting `TerminalSmtpException` will leave it stuck permanently.

# CLAUDE.md — Notifications Module

This module delivers transactional notifications to users and keeps a log of what was sent. It subscribes to integration events from other modules and sends email via `IEmailSender`.

---

## Domain vocabulary

- **NotificationLog** — a record that a notification was attempted or sent. Identified by `NotificationLogId`. Carries a `DeliveryStatus` (`Pending` / `Sent`) that tracks whether the SMTP send actually completed.
- **NotificationType** — an enum classifying the notification (`WelcomeEmail`, `PasswordResetRequest`, `PasswordResetConfirmation`, `PasswordChanged`, `EmailChangeRequest`, `EmailChanged`, `ExternalLoginPendingExistingUser`, `ExternalLoginPendingNewUser`, `ExternalLoginLinked`, `ExternalLoginUnlinked`).
- **NotificationDeliveryStatus** — `Pending = 0` (log row written, send not yet confirmed) / `Sent = 1` (send confirmed).
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

1. Every handler is idempotent. Each `NotificationLog` row carries a unique `IdempotencyKey` (sourced from `@event.EventId`) backed by a DB-level unique constraint. On a duplicate event delivery the insert throws `DbUpdateException` with a unique-constraint violation; the handler then checks `DeliveryStatus` and returns early only when it is `Sent` — meaning the email actually went out in a prior attempt.
2. `DeliveryStatus` tracks the real delivery outcome, not just whether the log row was written. The sequence is: insert log as `Pending` → send email → call `log.MarkSent()` + `SaveChangesAsync`. If `SendAsync` throws the row stays `Pending`; on the next retry the handler sees the `Pending` row and retries the send rather than silently dropping it.
3. `IConsentRegistry` gates every notification type. The welcome email checks `ConsentKeys.WelcomeEmail`. Security notifications (`PasswordReset*`, `PasswordChanged`, `EmailChange*`, `ExternalLogin*`) are transactional — they bypass consent because they are security-critical.
4. `PasswordResetRequestedV1`, `EmailChangeRequestedV1`, and `ExternalLoginPendingV1` carry a raw token — embed it in the email body link, never log it.

---

## Adding a new notification type

1. Add an entry to the `NotificationType` enum.
2. Add a template in `Templates/` (static class with `Subject`, `HtmlBody`, `PlainTextBody`).
3. Write a handler in `Integration/Subscribers/` subscribing to the triggering event. Follow the exact pattern below (copy from any existing handler).
4. Register the handler in `NotificationsModule.AddNotificationsHandlers`.

### Idempotency + delivery-status pattern

```csharp
var log = NotificationLog.Create(@event.UserId, @event.Email, NotificationType.Foo,
    FooTemplate.Subject, clock.UtcNow, @event.EventId);
db.NotificationLogs.Add(log);

try
{
    await db.SaveChangesAsync(ct);
}
catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
{
    db.Entry(log).State = EntityState.Detached;
    log = await db.NotificationLogs.FirstAsync(l => l.IdempotencyKey == @event.EventId, ct);
    if (log.DeliveryStatus == NotificationDeliveryStatus.Sent)
    {
        return;   // already delivered — truly idempotent
    }
    // DeliveryStatus is Pending: previous send failed. Fall through to retry.
}

await emailSender.SendAsync(message, ct);
log.MarkSent();
await db.SaveChangesAsync(ct);
```

Key rules:
- **Always detach the failed entity** (`db.Entry(log).State = EntityState.Detached`) before reloading it; otherwise Wolverine's `AutoApplyTransactions` middleware will try to re-insert the `Added` entity on the next `SaveChangesAsync`.
- **Only short-circuit on `Sent`**, not on the presence of the row. A `Pending` row means the previous attempt's SMTP call failed; retrying is correct.
- **Two explicit `SaveChangesAsync` calls** are intentional: the first commits the `Pending` log before touching SMTP, the second persists `MarkSent()`. Wolverine does not wrap these in a single outer DB transaction.

---

## Email infrastructure

`IEmailSender` → `SmtpEmailSender` (from `Shared.Infrastructure`). Configured from `Modules:Notifications:Smtp` section. Defaults to localhost:1025 (Mailpit in dev).

---

## Known footguns

- `SmtpEmailSender` is a real SMTP client — integration tests must override `IEmailSender` with a fake to avoid SMTP dial failures.
- Raw tokens arrive in `PasswordResetRequestedV1.RawToken`, `EmailChangeRequestedV1.RawToken`, and `ExternalLoginPendingV1.RawToken`. Embed them in email body links; never log them. Serilog destructuring masks known token property names, but defense-in-depth means not calling the log statement at all.
- The unique constraint on `NotificationLog.IdempotencyKey` makes duplicate-detection race-safe — catch `DbUpdateException.IsUniqueConstraintViolation()` rather than doing a pre-check with `AnyAsync`. But do NOT return early on the constraint alone; check `DeliveryStatus == Sent` first or you will silently drop mail after any transient SMTP failure.
- Adding a subscriber for a new event requires registering the handler in `NotificationsModule.AddNotificationsHandlers`; forgetting this means handlers are never discovered by Wolverine.

# ADR-0014: Two-Layer Notifications — Transport and Orchestration

## Status

Accepted

## Context

"Send an email when X happens" seems simple but expands into several concerns:

- Which transport (SMTP, SendGrid, SES, Twilio for SMS, push services)?
- Which template, which locale, which channel?
- What does the user prefer? Have they opted out?
- Delivery tracking — did it succeed, when, was it bounced?
- Rate limiting and digesting — don't send 50 emails in 5 minutes.
- Testability — local dev shouldn't send real emails.

Conflating these produces an `INotificationService` that tightly couples transport choice, template rendering, preference logic, and orchestration. Swapping SendGrid for SES means touching template code. Adding SMS means duplicating orchestration.

The correct separation:

1. **Transport** — how a rendered message physically gets sent.
2. **Orchestration** — what to send, to whom, through which channel, when.

## Decision

### Layer 1: Transport (Shared.Infrastructure)

Low-level, provider-adjacent:

```csharp
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

public interface ISmsSender
{
    Task SendAsync(SmsMessage message, CancellationToken ct);
}
```

Implementations:

- **Dev email**: points at **Mailpit**, provisioned by Aspire. Captures emails for local preview; no real delivery.
- **Dev SMS**: logs to console. No dev service ships in Aspire.
- **Prod**: teams wire their provider of choice (SendGrid, SES, Twilio, etc.). Not shipped in the template.

Transports know nothing about templates, preferences, or business events. They take a rendered message and deliver it.

### Layer 2: Orchestration (Notifications module)

A dedicated module (`Modulith.Modules.Notifications`) that owns:

- **Templates** — metadata in DB (which template for which event, active version, locale), content as Razor (`.cshtml`) embedded resources. Metadata + resources is the sweet spot: templates are diffable and PR-reviewable; metadata configures which template is used where.
- **User notification preferences** — a matrix of `(UserId, Category, Channel, Enabled)`. Categories: `Transactional`, `Security`, `Marketing`. Transactional and Security are non-opt-out by convention. Marketing is opt-in.
- **Notification log** — every notification dispatched, with `EventId`, `TemplateId`, `UserId`, `Channel`, `Status`, timestamps. Used for deduplication and for a user-facing "notification history" view.
- **Resolution pipeline** — for each incoming integration event: resolve which template, resolve user preferences, resolve channel, render, queue via Wolverine, invoke transport.
- **Idempotency** — handlers enforce `(EventId, TemplateId, UserId)` uniqueness to prevent duplicates on retry.

Other modules never call `IEmailSender` directly. They publish domain events (`UserRegistered`, `OrderPlaced`, `PasswordResetRequested`), and the Notifications module decides if and how to notify.

### Templating

**Razor**, via a lightweight renderer (`RazorLight` or a custom Razor runtime renderer):

- Type-safe models (each template has a record as its model type)
- Familiar syntax for .NET developers
- Content lives as embedded `.cshtml` resources
- DB metadata links events to template names and versions

### Consent integration

The Notifications module reads `ConsentRegistry` (from Users, ADR-0012) when a notification category requires consent. Marketing notifications always check consent. Transactional notifications do not (they are legitimate interest).

## Consequences

**Positive:**

- Transport choice is local — swapping SendGrid for SES changes one class.
- Notification logic lives with its data. Adding a new notification = adding a template + a subscriber, nothing else.
- User preferences are typed and central. One table, one read path.
- Mailpit in dev gives an immediate good experience. No account signups for first-run.
- Idempotent by design — redeliveries don't re-spam.

**Negative:**

- A module just for notifications. Some teams consider this overkill; this template optimizes for the team that eventually needs it.
- Razor templating ties to a renderer library. If `RazorLight` proves fragile, a Handlebars-based renderer is the fallback — the template-lookup layer is provider-agnostic.
- Eventually consistent with the source event. A user registers and the welcome email arrives seconds later. This is fine for notifications; not fine for things that should be synchronous (e.g., a password reset email the user is actively waiting for — handle with direct priority handlers).

## Related

- ADR-0003 (Wolverine): outbox delivers integration events to the Notifications module.
- ADR-0012 (GDPR): consent is integrated into the orchestration pipeline.

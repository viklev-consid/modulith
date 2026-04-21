# Example: Cross-Module Integration Event Subscriber

**Pattern:** Subscribe to a public event from another module, send an email, log it idempotently.

**Source:** `src/Modules/Notifications/Modulith.Modules.Notifications/Integration/Subscribers/OnUserRegisteredHandler.cs`

---

## The handler

```csharp
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Templates;
using Modulith.Modules.Users.Contracts;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Notifications.Integration.Subscribers;

public sealed class OnUserRegisteredHandler(   // Must be public — Wolverine rejects internal handler types
    NotificationsDbContext db,
    IEmailSender emailSender,
    IConsentRegistry consentRegistry,
    IClock clock)
{
    public async Task Handle(UserRegisteredV1 @event, CancellationToken ct)
    {
        // 1. Consent gate — don't send if the user hasn't consented
        if (!await consentRegistry.HasConsentedAsync(@event.UserId, ConsentKeys.WelcomeEmail, ct))
            return;

        // 2. Idempotency guard — safe to re-run on Wolverine retries
        var alreadySent = await db.NotificationLogs.AnyAsync(
            l => l.UserId == @event.UserId && l.NotificationType == NotificationType.WelcomeEmail,
            ct);

        if (alreadySent)
            return;

        // 3. Build and send the email
        var message = new EmailMessage(
            To: @event.Email,
            Subject: WelcomeEmailTemplate.Subject,
            HtmlBody: WelcomeEmailTemplate.HtmlBody(@event.DisplayName),
            PlainTextBody: WelcomeEmailTemplate.PlainTextBody(@event.DisplayName));

        await emailSender.SendAsync(message, ct);

        // 4. Log after sending — if this write fails, Wolverine retries step 3
        //    and the idempotency guard prevents a duplicate send
        var log = NotificationLog.Create(
            @event.UserId,
            @event.Email,
            NotificationType.WelcomeEmail,
            WelcomeEmailTemplate.Subject,
            clock.UtcNow);

        db.NotificationLogs.Add(log);
        await db.SaveChangesAsync(ct);
    }
}
```

---

## Anatomy

### Handler visibility

`public sealed class` — Wolverine requires handler types to be `public`. An `internal` handler compiles but throws `ArgumentOutOfRangeException: Handler types must be public` at startup.

### Consent gate

`IConsentRegistry.HasConsentedAsync` is implemented in the Users module (`UsersConsentRegistry`) and registered as a shared service. The Notifications module depends on the interface; it does not reach into the Users module directly.

Security notifications (password reset, email change) bypass this check — they are non-opt-out transactional notifications. Only marketing/preference-based notifications need consent gating.

### Idempotency guard

Wolverine retries handlers on failure. Sending an email twice is worse than skipping a retry, so this check runs before the send. The guard uses `(UserId, NotificationType)` as the dedup key. For notification types that recur legitimately (e.g., a second password-reset request), use a per-event dedup ID from the event payload instead.

### Send before log

Email is sent, then the log is written. If the log write fails, Wolverine retries the whole handler. The idempotency check then finds the previous send and skips. This ordering guarantees at-least-once delivery without double-sends.

### Referencing the publisher's Contracts project

The Notifications module's csproj references `Modulith.Modules.Users.Contracts` — not `Modulith.Modules.Users`. The boundary rule: only `.Contracts` projects may be referenced across modules.

```xml
<ProjectReference Include="..\..\Users\Modulith.Modules.Users.Contracts\Modulith.Modules.Users.Contracts.csproj" />
```

---

## Template structure

```
src/Modules/Notifications/Modulith.Modules.Notifications/
└── Integration/
    └── Subscribers/
        └── OnUserRegisteredHandler.cs     ← one file per event type
```

Each subscriber handles exactly one event type. Keep them small — if handling an event requires complex logic, extract it into a domain service or handler helper.

---

## Integration test

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task RegisteringUser_SendsWelcomeEmail()
{
    var session = await fixture.Host.TrackActivity()
        .Timeout(TimeSpan.FromSeconds(10))
        .ExecuteAndWaitAsync(async () =>
        {
            await client.PostAsJsonAsync("/v1/users/register", new RegisterRequest(...));
        });

    // Wolverine delivered the event and the handler ran
    session.Executed.SingleMessage<UserRegisteredV1>().ShouldNotBeNull();

    // The NotificationLog was written
    var log = await fixture.QueryDb<NotificationsDbContext>(db =>
        db.NotificationLogs.FirstOrDefaultAsync(l => l.NotificationType == NotificationType.WelcomeEmail));
    log.ShouldNotBeNull();
}
```

`TrackActivity().ExecuteAndWaitAsync` blocks until all cascading messages finish — no sleeps or polling.

---

## Related

- [`../how-to/cross-module-events.md`](../how-to/cross-module-events.md)
- [`../how-to/write-integration-test.md`](../how-to/write-integration-test.md)
- [`../adr/0003-wolverine-for-messaging.md`](../adr/0003-wolverine-for-messaging.md)
- [`../adr/0014-notifications-architecture.md`](../adr/0014-notifications-architecture.md)
- [`../adr/0020-no-idempotency-infrastructure.md`](../adr/0020-no-idempotency-infrastructure.md)

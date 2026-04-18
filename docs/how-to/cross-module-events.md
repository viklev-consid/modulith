# How-to: Cross-Module Events

When module A does something that module B cares about, the right mechanism is usually an integration event. This guide walks through publishing and subscribing.

For the reasoning, see [`adr/0005-module-communication-patterns.md`](../adr/0005-module-communication-patterns.md) and [`adr/0006-internal-vs-public-events.md`](../adr/0006-internal-vs-public-events.md).

---

## The two kinds of events

1. **Internal domain events** — raised by aggregates inside a module. Live in the module's `Domain/Events/` folder. May change freely.
2. **Public integration events** — published to other modules via the outbox. Live in the module's `.Contracts/Events/` folder. Versioned. Changes are breaking.

These are **separate types**. An internal handler in the publishing module maps one to the other.

---

## Publishing side

### 1. Define the internal domain event

```csharp
// src/Modules/Users/Modulith.Modules.Users/Domain/Events/UserEmailChanged.cs
namespace Modulith.Modules.Users.Domain.Events;

internal sealed record UserEmailChanged(UserId UserId, Email OldEmail, Email NewEmail) : DomainEvent;
```

Internal events use internal types (strongly-typed IDs, value objects). They are not wire-stable.

### 2. Raise it from the aggregate

```csharp
// Inside User aggregate
public Result ChangeEmail(Email newEmail)
{
    if (_email == newEmail) return Result.Ok();

    var oldEmail = _email;
    _email = newEmail;
    RaiseEvent(new UserEmailChanged(Id, oldEmail, newEmail));
    return Result.Ok();
}
```

Domain events are collected by the aggregate and flushed post-save by Wolverine middleware.

### 3. Define the public integration event

```csharp
// src/Modules/Users/Modulith.Modules.Users.Contracts/Events/UserEmailChangedV1.cs
namespace Modulith.Modules.Users.Contracts.Events;

public sealed record UserEmailChangedV1(
    Guid UserId,
    string NewEmail,
    DateTimeOffset OccurredAt);
```

Public events:

- Use primitive types (`Guid`, `string`), not domain types.
- Are versioned (`V1`, `V2`) — required by arch tests.
- Carry only what subscribers need. Don't leak internal data (`OldEmail` is usually not published).
- Include `OccurredAt` for ordering in eventually-consistent consumers.

### 4. Map internal to public

Write an internal handler in the publishing module:

```csharp
// src/Modules/Users/Modulith.Modules.Users/Integration/Publishers/PublishUserEmailChangedHandler.cs
namespace Modulith.Modules.Users.Integration.Publishers;

internal sealed class PublishUserEmailChangedHandler
{
    public async Task Handle(UserEmailChanged @event, IMessageBus bus, CancellationToken ct) =>
        await bus.PublishAsync(new UserEmailChangedV1(
            @event.UserId.Value,
            @event.NewEmail.Value,
            DateTimeOffset.UtcNow));
}
```

Wolverine picks this up automatically. The `PublishAsync` call enlists the message in the outbox; it will be published after the transaction commits.

---

## Subscribing side

### 1. Reference the publisher's Contracts project

In `Modulith.Modules.Notifications.csproj`:

```xml
<ProjectReference Include="..\..\Users\Modulith.Modules.Users.Contracts\Modulith.Modules.Users.Contracts.csproj" />
```

### 2. Write the subscriber handler

```csharp
// src/Modules/Notifications/Modulith.Modules.Notifications/Integration/Subscribers/OnUserEmailChangedHandler.cs
using Modulith.Modules.Users.Contracts.Events;

namespace Modulith.Modules.Notifications.Integration.Subscribers;

internal sealed class OnUserEmailChangedHandler
{
    private readonly NotificationsDbContext _db;

    public OnUserEmailChangedHandler(NotificationsDbContext db) => _db = db;

    public async Task Handle(UserEmailChangedV1 @event, CancellationToken ct)
    {
        // ... update the user's notification profile, send a confirmation, etc.
    }
}
```

Wolverine discovers the handler by assembly scanning. The handler will be invoked whenever a `UserEmailChangedV1` is published, regardless of which module published it.

### 3. Handle idempotency

The handler may be invoked more than once for the same event (retries, redeployments). Design accordingly:

- **State-based over delta-based.** `SetPreferredEmail(newEmail)` is safe on retry; `AppendEmailToHistory(newEmail)` is not.
- **Check before insert.** If the handler inserts rows, check for existence first or rely on unique constraints.
- **Skip silently if the operation is already done.** `await _db.Preferences.AnyAsync(...)` before writing.

See [`adr/0020-no-idempotency-infrastructure.md`](../adr/0020-no-idempotency-infrastructure.md).

---

## Testing cross-module flow

In an integration test, use Wolverine's `TrackActivity` to assert the end-to-end flow:

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task ChangingEmail_NotifiesNotificationsModule()
{
    // Arrange
    var user = await fixture.SeedAsync(UserMother.Active());
    var client = fixture.AuthenticatedClient().AsUser(user).Build();

    // Act — use TrackActivity to wait for all cascading messages
    var session = await fixture.Host.TrackActivity()
        .Timeout(TimeSpan.FromSeconds(10))
        .ExecuteAndWaitAsync(async () =>
        {
            var response = await client.PutAsJsonAsync(
                $"/v1/users/{user.Id.Value}/email",
                new ChangeEmailRequest("new@example.com"));
            response.EnsureSuccessStatusCode();
        });

    // Assert
    session.Executed.SingleMessage<UserEmailChangedV1>()
        .ShouldNotBeNull();
    session.Executed.Envelopes()
        .Any(e => e.Message is UserEmailChangedV1)
        .ShouldBeTrue();

    // Subscriber executed its handler
    var prefs = await fixture.QueryDb<NotificationsDbContext>(db =>
        db.UserPreferences.FindAsync(user.Id.Value));
    prefs!.Email.ShouldBe("new@example.com");
}
```

`TrackActivity` waits until all cascading messages finish processing before completing — no sleeping or polling needed.

---

## Versioning events

When a public event needs to change:

1. **Additive change (new optional field)**: version can stay the same but add the field with a default.
2. **Breaking change (rename, type change, required field removed)**: publish a new version (`UserEmailChangedV2`) and keep the old one for a deprecation window.
3. **Publishers emit both** for the deprecation window.
4. **Subscribers migrate to V2**, then V1 is retired.

Wolverine supports running both versions in parallel. Each version is a distinct type with its own handler(s).

---

## Common mistakes

- **Publishing the internal event directly.** The arch test catches this — internal events must not be public, and public events must be in `.Contracts/Events`.
- **Reusing domain types in public events.** `UserId` in a public event leaks internal types. Use `Guid`.
- **Forgetting the version suffix.** Arch test catches this.
- **Handler that isn't idempotent.** Works the first time, breaks on redelivery. No arch test can catch this — it's a design discipline.
- **Synchronous assumption on subscribers.** Eventually consistent by design. If a subscriber's state must be updated before the publisher's transaction commits, the design is wrong — do it in the publisher's handler.
- **Circular event dependencies between modules.** If A publishes X → B publishes Y → A subscribes to Y, you may have an event loop. Very rare but catastrophic when it happens. Detect with integration tests that measure message counts.

---

## Related

- [`add-a-slice.md`](add-a-slice.md)
- [`write-integration-test.md`](write-integration-test.md)
- [`adr/0003-wolverine-for-messaging.md`](../adr/0003-wolverine-for-messaging.md)
- [`adr/0005-module-communication-patterns.md`](../adr/0005-module-communication-patterns.md)
- [`adr/0006-internal-vs-public-events.md`](../adr/0006-internal-vs-public-events.md)

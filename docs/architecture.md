# Architecture

Modulith is a modular monolith: a single deployable unit composed of independent modules with strictly enforced boundaries. Each module is a vertical slice of business capability — it owns its domain, its persistence, its endpoints, and its public contracts.

The architecture is designed to be easy to split into separate services later, but equally designed to make that splitting unnecessary for a long time.

---

## The big picture

```
┌──────────────────────────────────────────────────────────────────────┐
│                          Aspire AppHost                              │
│     (orchestrates Postgres, Redis, Mailpit, API for local dev)       │
└──────────────────────────────────────────────────────────────────────┘
                                  │
                                  ▼
┌──────────────────────────────────────────────────────────────────────┐
│                              Modulith.Api                            │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  ASP.NET Core minimal APIs + versioning + rate limiting        │  │
│  │  JWT bearer auth + global exception handler + ProblemDetails   │  │
│  └────────────────────────────────────────────────────────────────┘  │
│                                  │                                    │
│                    Wolverine (in-process message bus)                │
│                    + durable outbox + scheduled jobs                  │
│                                  │                                    │
│  ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌──────────┐          │
│  │  Users   │   │ Catalog  │   │  Notifi- │   │  Audit   │   ...    │
│  │ Module   │   │  Module  │   │ cations  │   │  Module  │          │
│  │          │   │          │   │  Module  │   │          │          │
│  │  own DB  │   │  own DB  │   │  own DB  │   │  own DB  │          │
│  │  schema  │   │  schema  │   │  schema  │   │  schema  │          │
│  └──────────┘   └──────────┘   └──────────┘   └──────────┘          │
│       ▲              ▲              ▲              ▲                 │
│       └──────────────┴──────────────┴──────────────┘                 │
│              via public .Contracts projects only                      │
│                                                                       │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │  Shared.Kernel (primitives)  +  Shared.Infrastructure         │  │
│  │  (IBlobStore, IEmailSender, ICurrentUser, cache helpers)      │  │
│  └────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
```

---

## Solution layout

```
Modulith.sln
├── src/
│   ├── AppHost/
│   │   └── Modulith.AppHost.csproj                    # Aspire orchestration
│   ├── ServiceDefaults/
│   │   └── Modulith.ServiceDefaults.csproj            # Shared Aspire defaults
│   ├── Api/
│   │   └── Modulith.Api.csproj                        # Host + composition root
│   ├── Shared/
│   │   ├── Modulith.Shared.Kernel/                    # Result, DomainEvent, base types
│   │   ├── Modulith.Shared.Contracts/                 # Cross-module primitives
│   │   └── Modulith.Shared.Infrastructure/            # IBlobStore, IEmailSender, etc.
│   └── Modules/
│       ├── Users/
│       │   ├── Modulith.Modules.Users/                # Internal
│       │   └── Modulith.Modules.Users.Contracts/      # Public messages
│       ├── Catalog/
│       │   ├── Modulith.Modules.Catalog/
│       │   └── Modulith.Modules.Catalog.Contracts/
│       ├── Notifications/
│       │   ├── Modulith.Modules.Notifications/
│       │   └── Modulith.Modules.Notifications.Contracts/
│       └── Audit/
│           ├── Modulith.Modules.Audit/
│           └── Modulith.Modules.Audit.Contracts/
└── tests/
    ├── Modulith.Architecture.Tests/
    ├── Modulith.SmokeTests/
    ├── Modulith.TestSupport/
    └── Modules/
        ├── Users/
        │   ├── Modulith.Modules.Users.UnitTests/
        │   └── Modulith.Modules.Users.IntegrationTests/
        └── ...
```

### The reference rules

- `Api` references all modules' internal projects (to compose the application) and `ServiceDefaults`.
- A module's internal project references `Shared.Kernel`, `Shared.Infrastructure`, its own `.Contracts`, and other modules' `.Contracts` (only when it subscribes to their events).
- A module's `.Contracts` project references only `Shared.Kernel` and `Shared.Contracts`.
- `Shared.Kernel` references nothing.
- Tests reference the module they test, `TestSupport`, and test libraries.

Architectural tests enforce this. See [`adr/0015-architectural-tests.md`](adr/0015-architectural-tests.md).

---

## Inside a module

```
Modulith.Modules.Orders/
├── Domain/
│   ├── Order.cs                       # Aggregate root
│   ├── OrderLine.cs                   # Entity
│   ├── OrderId.cs                     # Strongly-typed ID
│   ├── OrderStatus.cs                 # Value object / enum
│   └── Events/
│       └── OrderPlaced.cs             # Internal domain event
├── Features/
│   ├── PlaceOrder/
│   │   ├── PlaceOrder.Request.cs
│   │   ├── PlaceOrder.Response.cs
│   │   ├── PlaceOrder.Command.cs
│   │   ├── PlaceOrder.Handler.cs
│   │   ├── PlaceOrder.Validator.cs
│   │   └── PlaceOrder.Endpoint.cs
│   ├── CancelOrder/
│   └── GetOrderById/
├── Integration/
│   └── UserDeactivatedHandler.cs      # Handles events from other modules
├── Persistence/
│   ├── OrdersDbContext.cs
│   ├── Configurations/
│   └── Migrations/
├── Seeding/
│   └── OrdersModuleSeeder.cs
└── OrdersModule.cs                    # AddOrdersModule / MapOrdersEndpoints
```

And in `Modulith.Modules.Orders.Contracts/`:

```
├── Events/
│   └── OrderPlaced.cs                 # Public integration event (separate from internal)
├── Commands/                          # If other modules can command this one
└── Queries/                           # If other modules can query this one
```

**Note:** internal domain events (`Domain/Events/`) and public integration events (`.Contracts/Events/`) are deliberately separate types. The internal event is raised inside an aggregate. A handler maps it to the public event and publishes the public one via the outbox. This decouples internal domain changes from the public contract. See [`adr/0006-internal-vs-public-events.md`](adr/0006-internal-vs-public-events.md).

---

## Request flow

Typical `POST /orders` request:

1. **ASP.NET Core** receives the HTTP request.
2. **Rate limiting middleware** applies the endpoint's policy.
3. **Authentication middleware** validates the JWT bearer token.
4. **Endpoint handler** (minimal API) receives the `Request` DTO and `IMessageBus`.
5. Endpoint maps `Request` → `Command`.
6. Endpoint calls `bus.InvokeAsync<Result<Response>>(command)`.
7. **Wolverine middleware pipeline** runs: validation (`FluentValidation`), transaction start, logging.
8. **Handler** executes: loads aggregates, invokes domain methods, returns `Result<T>`.
9. Transaction commits if the handler succeeded. Domain events are captured.
10. **Outbox middleware** persists outgoing integration events in the same transaction.
11. Wolverine returns the `Result<T>` to the endpoint.
12. Endpoint maps `Result<T>` → HTTP response (200/201 with body, or `ProblemDetails` on failure).
13. **Global exception handler** catches anything that escaped (bugs/infra) and returns a 500 `ProblemDetails`.

Post-commit, the outbox publishes integration events in the background. Subscribers in other modules handle them idempotently.

---

## Cross-module communication

Modules may communicate in three ways, all through `.Contracts`:

### 1. Integration events (most common)

Module A publishes a public event from its `.Contracts` project. Module B subscribes via a Wolverine handler in its `Integration/` folder. The outbox guarantees delivery. Use this for *notifications* — "something happened, react if you care".

### 2. Queries (less common)

Module A exposes a query in its `.Contracts` project. Module B sends the query via `IMessageBus` and awaits the response. Use this for *read data* that module B needs synchronously.

### 3. Commands (rarest)

Module A exposes a command in its `.Contracts` project. Module B sends the command. Use this only when module B legitimately needs to direct module A to do something. This is usually a sign the boundary is wrong — prefer events.

**What you may never do:**

- Reference another module's internal project
- Share a `DbContext` across modules
- Share a database schema across modules
- Inject another module's internal services
- Query another module's tables directly

See [`adr/0005-module-communication-patterns.md`](adr/0005-module-communication-patterns.md).

---

## The shared kernel

`Shared.Kernel` contains primitives used across modules:

- `Result<T>`, `Result`, and error types (via FluentResults or ErrorOr — decision deferred to ADR-0004)
- `DomainEvent` base type
- Strongly-typed ID base types (`TypedId<T>`)
- `IAuditableEntity` marker
- `[PersonalData]`, `[SensitivePersonalData]` attributes
- `ICurrentUser` abstraction

`Shared.Infrastructure` contains cross-cutting infrastructure:

- `IBlobStore` + local-disk implementation
- `IEmailSender` + `ISmsSender` with dev implementations
- `ICurrentUser` default implementation (reads from `HttpContext` or job context)
- Shared Wolverine middleware (audit, transaction, validation)
- Shared EF Core interceptors

The rule of thumb: if more than one module needs it and it has no domain meaning, it lives in `Shared.Infrastructure`. If it has domain meaning, it's a module.

---

## Cross-cutting concerns and where they live

| Concern | Location | ADR |
|---|---|---|
| Authentication (JWT validation) | `Api` + `Shared.Infrastructure` | 0007 |
| Authorization | Policies in `Api`, applied per-endpoint | 0007 |
| Validation (request-level) | `FluentValidation` per slice | 0008 |
| Validation (domain invariants) | Aggregate factory methods | 0009 |
| Logging | Serilog in `Api`, consumed everywhere | 0010 |
| Tracing/metrics | OTel via Aspire `ServiceDefaults` | 0010 |
| Caching | HybridCache, per-module key prefixes | 0017 |
| Rate limiting | ASP.NET built-in, per-endpoint policies | 0018 |
| Feature flags | Microsoft.FeatureManagement, edge only | 0019 |
| Audit (row-level) | `IAuditableEntity` + SaveChangesInterceptor | 0011 |
| Audit (change history) | Dedicated Audit module | 0011 |
| GDPR (data classification) | Attributes in `Shared.Kernel` | 0012 |
| GDPR (export/erase) | Per-module contracts | 0012 |
| Blob storage | `IBlobStore` in `Shared.Infrastructure` | 0013 |
| Notifications | Dedicated Notifications module | 0014 |

---

## Scaling out: when a module becomes a service

This template is designed to make that transition possible but unattractive. If you need to extract a module:

1. The module's `.Contracts` project becomes the wire contract (publish as NuGet, shared between both sides).
2. The module's DB schema moves to its own database.
3. Wolverine transports switch from in-process to a broker (RabbitMQ, Kafka, etc.) for that module's events.
4. The module gets its own host.

Because of the boundary rules, this is a *mechanical* change rather than a re-architecture. But most teams never need to do it, because the monolith scales vertically for a long time and operationally is a fraction of the cost of a distributed system.

---

## Further reading

- [`glossary.md`](glossary.md) — terms used throughout
- [`testing-strategy.md`](testing-strategy.md) — how we verify this architecture holds
- [`agentic-development.md`](agentic-development.md) — how this template is set up for AI agents
- [`adr/`](adr/) — the reasoning behind every decision
- [`how-to/`](how-to/) — practical guides

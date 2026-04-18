# Glossary

Terms as used in this codebase. Some terms are industry-standard; others are codebase-specific. When a term is ambiguous in general DDD/architecture literature, we pick a specific meaning and use it consistently.

---

**Aggregate.** A cluster of domain objects treated as a single unit for data changes. Has a root entity (the *aggregate root*) that is the only entry point for modifications. Invariants that span multiple objects are enforced at the aggregate boundary. In this codebase, aggregates are found under a module's `Domain/` folder.

**Aggregate Root.** The single entity that external code interacts with to modify an aggregate. All changes to the aggregate go through its methods. Has a strongly-typed ID. No public setters.

**API Versioning.** Exposing different versions of endpoints (`/v1/`, `/v2/`) to allow backward-compatible evolution. Implemented via `Asp.Versioning.Http`.

**Architectural Test.** A test that verifies structural rules of the codebase (who depends on whom, where types live, naming conventions). Implemented with `NetArchTest`. Runs as part of the fast test tier.

**Audit Module.** A dedicated module that consumes domain events from other modules and persists a change history. Separate from row-level audit fields (which are per-entity).

**Auditable Entity.** An entity implementing `IAuditableEntity`, which carries `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`. Populated automatically by a `SaveChangesInterceptor`.

**AuthN / AuthZ.** Authentication (who are you) and Authorization (what you may do). We use JWT bearer authentication with a lightweight `User` aggregate — not ASP.NET Identity.

**Blob.** Binary content (file, image, PDF, etc.) stored outside the relational database. Accessed via `IBlobStore`. Each blob has an opaque `BlobRef` used for retrieval.

**Bootstrap Logger.** A minimal Serilog logger configured in `Program.cs` before the host is built, used to capture startup errors that happen before configuration-based logging is initialized.

**Bus.** Short for message bus. In this codebase, `IMessageBus` is Wolverine's abstraction for sending commands and publishing events.

**Capability Boundary.** An explicit statement (usually in `CLAUDE.md`) of what an agent should not modify without asking. See the agent operating manual.

**Command.** A request to change state. Named imperatively (`PlaceOrder`, `CancelOrder`). Handled by exactly one handler. Returns `Result<T>`. Records, not classes.

**Composition Root.** The single place where dependency injection registrations are wired up. In this codebase, `Api/Program.cs` plus each module's `AddXxxModule` extension.

**Contract.** A type in a module's `.Contracts` project — a public command, query, event, or DTO that other modules may depend on. Contracts are the module's API surface. Changing a contract is a breaking change.

**Consent.** A record of a user granting or revoking permission for a specific purpose (marketing emails, data processing). Stored in the Users module. Separate from notification preferences, though wired together.

**Cross-cutting Concern.** A concern that affects multiple modules — logging, caching, authentication, rate limiting. Lives in `Shared.Infrastructure`, `Api`, or as a dedicated module if it has its own data.

**Destructuring Policy.** A Serilog configuration that controls how objects are serialized into log events. Used to mask sensitive properties.

**Domain Event.** An event raised from within an aggregate when something business-meaningful happens (`OrderPlaced`, `UserDeactivated`). Internal to the module. Distinct from *integration events*, which are the public version published to other modules via the outbox.

**DTO.** Data Transfer Object. A type whose purpose is to carry data across a boundary — HTTP (Request/Response), cross-module (Contracts). Has no behavior.

**Endpoint.** An HTTP route handler. In this codebase, a minimal API delegate that maps a `Request` to a `Command`, dispatches via `IMessageBus`, and maps the `Result<T>` to an HTTP response. Lives in the slice folder.

**Enricher.** A Serilog component that adds properties to every log event (machine name, environment, span ID). Configured in `Program.cs`.

**Feature Flag.** A named boolean (or richer value) that controls whether a code path is active. Two lifetimes: *startup* (module toggles, read from config via `IOptions`) and *runtime* (`IFeatureManager`).

**Feature Slice.** See Slice.

**Global Exception Handler.** An implementation of `IExceptionHandler` that catches unhandled exceptions at the pipeline boundary and converts them to `ProblemDetails` responses. For expected failures, use `Result` instead.

**GDPR Primitives.** The set of types and contracts baked into the template to support GDPR compliance: classification attributes, exporter/eraser contracts, consent tracking, retention hooks.

**Handler.** A class that processes a command, query, or event. Discovered by Wolverine, invoked via `IMessageBus`. Handlers orchestrate — they load aggregates, invoke domain methods, commit via the UoW, and return results.

**HybridCache.** .NET 10's built-in two-tier cache (in-memory L1 + distributed L2). Replaces the `IMemoryCache` + `IDistributedCache` combination. Used with Redis via Aspire.

**Idempotent.** An operation that can be executed multiple times with the same effect as executing it once. Critical for message handlers in an at-least-once delivery system.

**Integration Event.** A public event published to other modules via the outbox. Defined in a module's `.Contracts` project. Distinct from internal *domain events*.

**Integration Test.** A test that exercises a full slice end-to-end (HTTP → handler → DB) using a real database via Testcontainers. Per-module, lives in `tests/Modules/<Module>/*.IntegrationTests/`.

**Invariant.** A rule that must always be true for the domain to be in a valid state. Enforced by aggregate methods. Violations produce `Result.Fail`, not exceptions.

**Kernel.** See Shared Kernel.

**Mailpit.** A development SMTP server that captures emails for local preview. Run by Aspire in dev. Replaces SendGrid/SES/etc. for local development.

**Message Bus.** See Bus.

**Minimal API.** ASP.NET Core's lightweight endpoint style (`app.MapPost(...)` with delegates). Preferred over controllers in this codebase because it pairs cleanly with per-slice endpoint files.

**Modular Monolith.** A single deployable unit composed of internally modular components with enforced boundaries. Not microservices — one host, one process. Not a traditional monolith — internal modules cannot reach across each other.

**Module.** A vertical slice of business capability. Has its own domain, persistence (schema), endpoints, and public contracts. Examples: Users, Orders, Catalog, Audit, Notifications.

**Notification.** A message sent to a user via email, SMS, or push. Orchestrated by the Notifications module, which owns templates, user preferences, and a delivery log.

**Observability.** Logs, metrics, and traces that let you understand what the system is doing. Powered by OpenTelemetry, surfaced in the Aspire dashboard locally.

**Outbox.** A pattern where messages to publish are written to the database in the same transaction as the state change, then a background process publishes them. Guarantees at-least-once delivery with transactional consistency. Provided by Wolverine.

**Personal Data.** User data subject to GDPR. Marked with the `[PersonalData]` attribute. Affects logging (masked), export (included in personal data export), and erasure.

**ProblemDetails.** The RFC 7807 standard response format for HTTP errors. All error responses in this API use ProblemDetails.

**Query.** A request to read state without changing anything. Named descriptively (`GetOrderById`). Handled by exactly one handler. Returns `Result<T>` where T is a Response DTO. Records, not classes.

**Rate Limiting.** Restricting the number of requests a client may make in a time window. Applied per-endpoint via policies (auth, write, read, expensive). ASP.NET Core built-in.

**Redis.** An in-memory data store used as the L2 cache for HybridCache and for Wolverine's durable messaging (optionally). Provisioned by Aspire.

**Request.** The HTTP request DTO for an endpoint. Public contract with external clients. Lives in the slice folder as `{Slice}.Request.cs`.

**Response.** The HTTP response DTO for an endpoint. Public contract with external clients. Lives in the slice folder as `{Slice}.Response.cs`.

**Result Pattern.** Returning `Result<T>` from operations that can fail for expected reasons, rather than throwing exceptions. Makes failure paths explicit in the type system.

**Scalar.** An OpenAPI documentation UI. Replacement for Swagger UI. Pairs cleanly with .NET 10's built-in OpenAPI generation.

**Seeder.** An implementation of `IModuleSeeder` that populates a module's database with deterministic local-dev data on first run. Not used in production.

**Serilog.** The structured logging library used throughout. Configured via `appsettings.json` with a bootstrap fallback in `Program.cs`. Writes to OpenTelemetry so logs correlate with traces.

**ServiceDefaults.** An Aspire convention: a shared project that wires up OTel, health checks, HTTP resilience, and service discovery for the host. Each host project calls `AddServiceDefaults()`.

**Shared Infrastructure.** The project `Shared.Infrastructure` — cross-cutting infrastructure with no domain meaning: `IBlobStore`, `IEmailSender`, `ICurrentUser`, shared interceptors, shared Wolverine middleware.

**Shared Kernel.** The project `Shared.Kernel` — domain-adjacent primitives: `Result<T>`, `DomainEvent`, strongly-typed IDs, classification attributes. Has no runtime dependencies beyond the BCL.

**Slice.** A feature folder inside a module. Contains all files for a single feature: `Request`, `Response`, `Command`/`Query`, `Handler`, `Validator`, `Endpoint`. Co-located to reduce the cost of change.

**Smoke Test.** A test that spins up the full Aspire stack and exercises a real endpoint end-to-end. Small number, runs in release CI. Uses `Aspire.Hosting.Testing`.

**Testcontainers.** A library for spinning up ephemeral Docker containers during tests. Used for real Postgres (and Redis, if needed) in integration tests. No in-memory EF, no SQLite stand-in.

**TestSupport.** A test project containing shared fixtures, builders, authenticated client helpers, and object mothers. Referenced by all module test projects.

**Transport.** The layer that physically delivers a notification: `IEmailSender`, `ISmsSender`. Lives in `Shared.Infrastructure`. Distinct from orchestration (templates, preferences), which is the Notifications module's job.

**Two-Phase Commit (blob lifecycle).** The pattern for blob uploads in this template: upload succeeds → publish `BlobUploaded` event → handler references the blob in a domain operation → publish `BlobCommitted` event. Uncommitted blobs are swept by a background job.

**Unit of Work.** The EF Core `DbContext` scoped to a request. Wolverine's `AutoApplyTransactions` wraps each handler in a transaction against the appropriate module's `DbContext`.

**Validator.** A `FluentValidation` validator for a Request or Command. Runs via Wolverine middleware before the handler executes. Lives in the slice folder as `{Slice}.Validator.cs`.

**Vertical Slice.** See Slice. The architecture term for organizing code by feature rather than by layer.

**Wolverine.** The in-process message bus + durable outbox + background job scheduler used throughout. Replaces the combination of MediatR, Hangfire, and MassTransit.

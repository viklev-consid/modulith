# Glossary

Terms as used in this codebase. Some terms are industry-standard; others are codebase-specific. When a term is ambiguous in general DDD/architecture literature, we pick a specific meaning and use it consistently.

---

**Aggregate.** A cluster of domain objects treated as a single unit for data changes. Has a root entity (the *aggregate root*) that is the only entry point for modifications. Invariants that span multiple objects are enforced at the aggregate boundary. In this codebase, aggregates are found under a module's `Domain/` folder.

**Aggregate Root.** The single entity that external code interacts with to modify an aggregate. All changes to the aggregate go through its methods. Has a strongly-typed ID. No public setters.

**API Versioning.** Exposing different versions of endpoints (`/v1/`, `/v2/`) to allow backward-compatible evolution. Implemented via `Asp.Versioning.Http`.

**Architectural Test.** A test that verifies structural rules of the codebase (who depends on whom, where types live, naming conventions). Implemented with `NetArchTest`. Runs as part of the fast test suite.

**Audit Module.** A dedicated module that consumes domain events from other modules and persists a change history. Separate from row-level audit fields (which are per-entity).

**Auditable Entity.** An entity implementing `IAuditableEntity`, which carries `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`. Populated automatically by a `SaveChangesInterceptor`.

**AuthN / AuthZ.** Authentication (who are you) and Authorization (what you may do). We use JWT bearer authentication with a lightweight `User` aggregate — not ASP.NET Identity.

**At-least-once Delivery.** A message-delivery guarantee where the sender ensures a message is published at least once, even if retries mean a consumer may see the same message more than once. Wolverine's outbox provides at-least-once producer delivery, so subscribers must be idempotent.

**Blob.** Binary content (file, image, PDF, etc.) stored outside the relational database. Accessed via `IBlobStore`. Each blob has an opaque `BlobRef` used for retrieval.

**Bootstrap Logger.** A minimal Serilog logger configured in `Program.cs` before the host is built, used to capture startup errors that happen before configuration-based logging is initialized.

**Bus.** Short for message bus. In this codebase, `IMessageBus` is Wolverine's abstraction for sending commands and publishing events.

**Capability Boundary.** An explicit statement (usually in `CLAUDE.md`) of what an agent should not modify without asking. See the agent operating manual.

**Command.** A request to change state. Named imperatively (`PlaceOrder`, `CancelOrder`). Handled by exactly one feature handler; in this codebase those handlers return `ErrorOr<T>`. Records, not classes.

**Composition Root.** The single place where dependency injection registrations are wired up. In this codebase, `Api/Program.cs` plus each module's `AddXxxModule` extension.

**Contract.** A type in a module's `.Contracts` project — a public command, query, event, or DTO that other modules may depend on. Contracts are the module's API surface. Changing a contract is a breaking change.

**Consent.** A record of a user granting or revoking permission for a specific purpose (marketing emails, data processing). Stored in the Users module. Separate from notification preferences, though wired together. Distinct from *Terms Acceptance* — consent gates optional behaviour (e.g. marketing), whereas terms acceptance records legal agreement to a document version.

**Credential Retention Guardrail.** A domain invariant enforced by `User.UnlinkExternalLogin`: a user must always retain at least one login credential (a password or another external login). Unlinking the last credential returns a `Conflict` error, preventing account lock-out.

**Cross-cutting Concern.** A concern that affects multiple modules — logging, caching, authentication, rate limiting. Lives in `Shared.Infrastructure`, `Api`, or as a dedicated module if it has its own data.

**Destructuring Policy.** A Serilog configuration that controls how objects are serialized into log events. Used to mask sensitive properties.

**Domain Event.** An event raised from within an aggregate when something business-meaningful happens (`OrderPlaced`, `UserDeactivated`). Internal to the module and not wire-stable. Distinct from *integration events*, which are the public version published to other modules via the outbox.

**DTO.** Data Transfer Object. A type whose purpose is to carry data across a boundary — HTTP (Request/Response), cross-module (Contracts). Has no behavior.

**Email Loop.** The flow triggered when a Google ID token cannot be fast-pathed (i.e. the `(provider, subject)` pair is not yet linked to a user). The server creates a `PendingExternalLogin`, sends an email containing the raw confirmation token, and returns 202. The client has no way to distinguish "email unknown" from "email known but not linked" — both return identical 202 responses, preventing account enumeration. The loop completes when the user clicks the link and calls the confirm endpoint.

**Endpoint.** An HTTP route handler. In this codebase, a minimal API delegate that maps a `Request` to a `Command`, dispatches via `IMessageBus`, and maps `ErrorOr<T>` to an HTTP response. Lives in the slice folder.

**Enricher.** A Serilog component that adds properties to every log event (machine name, environment, span ID). Configured in `Program.cs`.

**ErrorOr.** The result-type library used to implement the Result Pattern in this codebase. Handlers and many domain factory methods return `ErrorOr<T>` for expected failures, and shared HTTP extensions map those failures to `ProblemDetails` responses.

**External Login.** An entity linking an OAuth provider identity `(Provider, Subject)` to a local `User`. Stored as a child collection on the `User` aggregate. The `(Provider, Subject)` pair is globally unique — one Google account can only be linked to one local user. Distinct from a password credential; a user may hold both simultaneously.

**Eventually Consistent.** A state where changes become visible across module boundaries asynchronously rather than in the publisher's transaction. Integration events, cache invalidation, audit trails, and GDPR erasure workflows are eventually consistent by design.

**Feature Flag.** A named boolean (or richer value) that controls whether a code path is active. Two lifetimes: *startup* (module toggles, read from config via `IOptions`) and *runtime* (`IFeatureManager`).

**Feature Slice.** See Slice.

**Global Exception Handler.** An implementation of `IExceptionHandler` that catches unhandled exceptions at the pipeline boundary and converts them to `ProblemDetails` responses. For expected failures, return an `ErrorOr` failure instead.

**GDPR Primitives.** The set of types and contracts baked into the template to support GDPR compliance: classification attributes, exporter/eraser contracts, consent tracking, retention hooks.

**JWKS (JSON Web Key Set).** A JSON document published by an identity provider (e.g. `https://www.googleapis.com/oauth2/v3/certs`) that contains the public signing keys used to verify ID tokens. Fetched by `GoogleIdTokenVerifier` and cached in `IMemoryCache`. Verification is fail-closed: if the JWKS endpoint is unreachable, the verifier returns `ExternalAuthUnavailable` rather than treating the token as valid.

**Handler.** A public class that processes a command, query, or event. Discovered by Wolverine, invoked via `IMessageBus`. Feature handlers orchestrate — they load aggregates, invoke domain methods, commit via the UoW, and return `ErrorOr<T>`. Integration-event subscribers usually return `Task` and apply side effects within their own module.

**HybridCache.** .NET 10's built-in two-tier cache (in-memory L1 + distributed L2). Replaces the `IMemoryCache` + `IDistributedCache` combination. Used with Redis via Aspire.

**Idempotent.** An operation that can be executed multiple times with the same effect as executing it once. Critical for message handlers in an at-least-once delivery system.

**Integration Event.** A public, wire-stable event published to other modules via the outbox. Defined in a module's `.Contracts` project and usually versioned (`UserRegisteredV1`). Distinct from internal *domain events*.

**Integration Test.** A test that exercises a full slice end-to-end (HTTP → handler → DB) using a real database via Testcontainers. Per-module, lives in `tests/Modules/<Module>/*.IntegrationTests/`.

**Invariant.** A rule that must always be true for the domain to be in a valid state. Enforced by aggregate methods. Violations return an `ErrorOr` failure, not exceptions.

**Kernel.** See Shared Kernel.

**Mailpit.** A development SMTP server that captures emails for local preview. Run by Aspire in dev. Replaces SendGrid/SES/etc. for local development.

**Message Bus.** See Bus.

**Minimal API.** ASP.NET Core's lightweight endpoint style (`app.MapPost(...)` with delegates). Preferred over controllers in this codebase because it pairs cleanly with per-slice endpoint files.

**Modular Monolith.** A single deployable unit composed of internally modular components with enforced boundaries. Not microservices — one host, one process. Not a traditional monolith — internal modules cannot reach across each other.

**Module.** A vertical slice of business capability. Has its own domain, persistence (schema), endpoints, and public contracts. Examples: Users, Orders, Catalog, Audit, Notifications.

**Notification.** A message sent to a user via email, SMS, or push. Orchestrated by the Notifications module, which owns templates, user preferences, and a delivery log.

**Object Mother.** A test helper that creates valid, domain-specific objects with realistic defaults (`UserMother.Active()`, `OrderMother.PlacedWithItems(3)`). Lives in `TestSupport.TestDataBuilders/` and is preferred over manually seeding raw entities in tests.

**Observability.** Logs, metrics, and traces that let you understand what the system is doing. Powered by OpenTelemetry, surfaced in the Aspire dashboard locally.

**Outbox.** A pattern where messages to publish are written to the database in the same transaction as the state change, then a background process publishes them. Guarantees at-least-once delivery with transactional consistency. Provided by Wolverine.

**Pending External Login.** A transient, pre-account record created during the *Email Loop*. Not linked to a `User` (the user may not exist yet). Holds the `(Provider, Subject, Email)` tuple, a SHA-256 hash of the raw confirmation token, and an `IsExistingUser` flag that was captured at creation time for deterministic email template selection. Single-use via `Consume(IClock)`; considered invalid once consumed or expired. Swept from the database after expiry by `SweepExpiredTokensHandler`.

**Personal Data.** User data subject to GDPR. Usually marked with `[PersonalData]` or `[SensitivePersonalData]`; modules that reference a user without storing personal data can opt out with `[NoPersonalData]`. Classification affects logging (masked), export (included in personal data export), and erasure.

**ProblemDetails.** The RFC 7807 standard response format for HTTP errors. All error responses in this API use ProblemDetails.

**Query.** A request to read state without changing anything. Named descriptively (`GetOrderById`). Handled by exactly one feature handler. Feature handlers return `ErrorOr<T>` where `T` is usually a Response DTO. Records, not classes.

**Rate Limiting.** Restricting the number of requests a client may make in a time window. Applied per-endpoint via policies (auth, write, read, expensive). ASP.NET Core built-in.

**Redis.** An in-memory data store used as the L2 cache for HybridCache and for Wolverine's durable messaging (optionally). Provisioned by Aspire.

**Request.** The HTTP request DTO for an endpoint. Public contract with external clients. Lives in the slice folder as `{Slice}.Request.cs`.

**Respawn.** A library used in integration tests to wipe database state between tests without recreating the schema. The module fixtures create the database once per test class; Respawn resets it between test cases.

**Response.** The HTTP response DTO for an endpoint. Public contract with external clients. Lives in the slice folder as `{Slice}.Response.cs`.

**Result Pattern.** Returning a result type rather than throwing exceptions for expected failures. In this codebase the concrete type is `ErrorOr<T>`, which makes validation, not-found, conflict, and business-rule failures explicit in method signatures.

**Scalar.** An OpenAPI documentation UI. Replacement for Swagger UI. Pairs cleanly with .NET 10's built-in OpenAPI generation.

**Scheduled Job.** A time-based Wolverine message handler scheduled for future execution. Used for delayed or recurring work such as cleanup sweeps, retries, and token-expiry processing.

**Seeder.** An implementation of `IModuleSeeder` that populates a module's database with deterministic local-dev data on first run. Not used in production.

**SensitivePersonalData.** A stricter GDPR classification attribute than `[PersonalData]`, used for fields that need tighter handling or masking. It participates in the same export and erasure flows while signaling stronger protection requirements.

**Serilog.** The structured logging library used throughout. Configured via `appsettings.json` with a bootstrap fallback in `Program.cs`. Writes to OpenTelemetry so logs correlate with traces.

**ServiceDefaults.** An Aspire convention: a shared project that wires up OTel, health checks, HTTP resilience, and service discovery for the host. Each host project calls `AddServiceDefaults()`.

**Shared Infrastructure.** The project `Shared.Infrastructure` — cross-cutting infrastructure with no domain meaning: `IBlobStore`, `IEmailSender`, `ICurrentUser`, shared interceptors, shared Wolverine middleware.

**Shared Kernel.** The project `Shared.Kernel` — domain-adjacent primitives such as `DomainEvent`, strongly-typed IDs, pagination types, GDPR classification attributes, and shared abstractions like `IClock`, `ICurrentUser`, `IPersonalDataExporter`, and `IPersonalDataEraser`. Has no runtime dependencies beyond the BCL.

**Slice.** A feature folder inside a module. Contains all files for a single feature: `Request`, `Response`, `Command`/`Query`, `Handler`, `Validator`, `Endpoint`. Co-located to reduce the cost of change.

**Smoke Test.** A test that spins up the full Aspire stack and exercises a real endpoint end-to-end. Small number, runs in release CI. Uses `Aspire.Hosting.Testing`.

**Testcontainers.** A library for spinning up ephemeral Docker containers during tests. Used for real Postgres (and Redis, if needed) in integration tests. No in-memory EF, no SQLite stand-in.

**TestSupport.** A test project containing shared fixtures and helpers such as `ApiTestFixture`, `AuthenticatedClientBuilder`, object mothers, Verify settings, test clocks, and HTTP stubs. Referenced by all module test projects.

**Terms Acceptance.** An immutable record that a user accepted a specific version of a legal document (e.g. `"tos:1.0"`). Created by the `CompleteOnboarding` slice, which writes a single ToS row keyed to the current `UsersOptions.TermsOfServiceVersion`. Keyed by `(UserId, Version)` with a unique constraint — re-submitting `CompleteOnboarding` when a row for the current version already exists is a no-op. Distinct from *Consent*, which gates optional processing (e.g. `marketing-emails`); terms acceptance is required for account activation.

**Transport.** The layer that physically delivers a notification: `IEmailSender`, `ISmsSender`. Lives in `Shared.Infrastructure`. Distinct from orchestration (templates, preferences), which is the Notifications module's job.

**Two-Phase Commit (blob lifecycle).** The pattern for blob uploads in this template: upload succeeds → publish `BlobUploaded` event → handler references the blob in a domain operation → publish `BlobCommitted` event. Uncommitted blobs are swept by a background job.

**Unit of Work.** The EF Core `DbContext` scoped to a request. Wolverine's `AutoApplyTransactions` wraps each handler in a transaction against the appropriate module's `DbContext`.

**Validator.** A `FluentValidation` validator for a Request or Command. Runs via Wolverine middleware before the handler executes. Lives in the slice folder as `{Slice}.Validator.cs`.

**Vertical Slice.** See Slice. The architecture term for organizing code by feature rather than by layer.

**Wire-stable.** Safe to serialize across module or HTTP boundaries without depending on internal types or semantics that may change freely. Integration events and public contracts must be wire-stable; domain events are not.

**Wolverine.** The in-process message bus + durable outbox + background job scheduler used throughout. Replaces the combination of MediatR, Hangfire, and MassTransit.

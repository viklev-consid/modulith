# Implementation Roadmap

This document is the sequence for building Modulith. It is designed to be read top-to-bottom by Claude Code (or a human with time and patience) to produce a working template.

The principle: **build in increments where each increment is independently testable and produces verifiable output**. No building for weeks with nothing to run.

---

## Prerequisites before starting

Installed:

- .NET 10 SDK (or latest preview at time of implementation)
- Docker (for Testcontainers + Aspire dev stack)
- `dotnet-ef` global tool: `dotnet tool install --global dotnet-ef`
- `dotnet-aspire` templates: `dotnet new install Aspire.ProjectTemplates`

Familiarity with:

- [`README.md`](README.md)
- [`CLAUDE.md`](CLAUDE.md)
- [`docs/architecture.md`](docs/architecture.md)
- ADRs 0001-0005 and 0015 at minimum

---

## Phase 0: Solution skeleton

**Goal:** A solution that builds, with all project structure in place, no business logic yet.

### 0.1 Create solution and top-level files

```bash
mkdir modulith && cd modulith
dotnet new sln -n Modulith
```

Create at the repo root:

- `Directory.Packages.props` — central package management (ADR-0016)
- `Directory.Build.props` — shared MSBuild properties (ADR-0016)
- `.editorconfig` — style and analyzer severities
- `.globalconfig` — analyzer severities that can't be in `.editorconfig`
- `.gitignore` — standard .NET ignore + `appsettings.Local.json`
- `global.json` — pin the SDK version

`Directory.Build.props` should include:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>14.0</LangVersion>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <AnalysisMode>AllEnabledByDefault</AnalysisMode>
  </PropertyGroup>
</Project>
```

### 0.2 Create Aspire AppHost and ServiceDefaults

```bash
dotnet new aspire-apphost -n Modulith.AppHost -o src/AppHost
dotnet new aspire-servicedefaults -n Modulith.ServiceDefaults -o src/ServiceDefaults
dotnet sln add src/AppHost src/ServiceDefaults
```

### 0.3 Create Shared projects

```bash
dotnet new classlib -n Modulith.Shared.Kernel -o src/Shared/Modulith.Shared.Kernel
dotnet new classlib -n Modulith.Shared.Contracts -o src/Shared/Modulith.Shared.Contracts
dotnet new classlib -n Modulith.Shared.Infrastructure -o src/Shared/Modulith.Shared.Infrastructure
dotnet sln add src/Shared/Modulith.Shared.Kernel src/Shared/Modulith.Shared.Contracts src/Shared/Modulith.Shared.Infrastructure
```

### 0.4 Create API project

```bash
dotnet new webapi -n Modulith.Api -o src/Api --use-minimal-apis
dotnet sln add src/Api
```

Reference `ServiceDefaults` and the Shared projects.

### 0.5 Create test infrastructure projects

```bash
dotnet new classlib -n Modulith.TestSupport -o tests/Modulith.TestSupport
dotnet new xunit -n Modulith.Architecture.Tests -o tests/Modulith.Architecture.Tests
dotnet new xunit -n Modulith.SmokeTests -o tests/Modulith.SmokeTests
```

Note: use xUnit v3 when the template supports it (or upgrade the generated projects to v3).

### 0.6 Verify

`dotnet build` succeeds. No warnings. `dotnet run --project src/AppHost` boots an empty Aspire dashboard.

**Commit.** Message: "chore: solution skeleton with Aspire, shared projects, and test infrastructure".

---

## Phase 1: Shared primitives

**Goal:** `Shared.Kernel` and `Shared.Infrastructure` have the primitives every module will use.

### 1.1 Shared.Kernel

Implement:

- `Result`, `Result<T>`, `Error` (via ErrorOr — reference the ErrorOr package, re-export types from the kernel for convenience, or adopt ErrorOr's types directly)
- `TypedId<T>` base for strongly-typed IDs
- `AggregateRoot<TId>` and `Entity<TId>` base classes with domain event tracking
- `DomainEvent` base record
- `[PersonalData]`, `[SensitivePersonalData]`, `[NoPersonalData]` attributes
- `IAuditableEntity` interface
- `IRetainable` interface
- `ICurrentUser` interface
- `IClock` interface
- `IPersonalDataExporter`, `IPersonalDataEraser` interfaces
- `UserRef`, `PersonalDataExport`, `ErasureResult`, `ErasureStrategy` records

### 1.2 Shared.Infrastructure

Implement:

- `ModuleDbContext` base (applies audit interceptor, snake_case naming, UTC dates)
- `AuditableEntitySaveChangesInterceptor`
- `CurrentUser` default (reads from `HttpContext.User`)
- `SystemClock` default `IClock`
- `IBlobStore` interface, `BlobRef`, `BlobMetadata`, `BlobContent` records
- `LocalDiskBlobStore` reference implementation (GUID keys, sidecar metadata, token-based download)
- `IEmailSender`, `ISmsSender` interfaces
- `SmtpEmailSender` implementation
- `LoggingSmsSender` (dev default)
- Shared Serilog setup extensions
- Shared Wolverine middleware (audit, validation, caching invalidation)
- Serilog destructuring policy for `[PersonalData]` / `[SensitivePersonalData]` / name-pattern masking

### 1.3 Unit tests for shared primitives

Write tests for:

- `Result` / `Error` behavior
- `TypedId<T>` equality
- `AggregateRoot` event tracking
- Destructuring policy masks classified properties
- `LocalDiskBlobStore` put/get/delete/url lifecycle

### 1.4 Verify

`dotnet build` succeeds. `dotnet test tests/` passes.

**Commit.** Message: "feat: shared kernel and infrastructure primitives".

---

## Phase 2: API host composition

**Goal:** API boots, middleware pipeline wired, no modules yet.

### 2.1 Program.cs composition

Wire in this order:

1. `AddServiceDefaults()` (Aspire — OTel, health, resilience)
2. Serilog via `appsettings.json` with OTel sink
3. `AddProblemDetails()`
4. `AddAuthentication().AddJwtBearer(...)` (symmetric key from user-secrets for dev)
5. `AddAuthorization()` with baseline policies
6. `AddOpenApi()` + Scalar
7. `AddApiVersioning()`
8. `AddRateLimiter(...)` with tiered policies (auth/write/read/expensive/global)
9. `AddHybridCache()` with Redis
10. `AddFeatureManagement().WithTargeting<CurrentUserTargetingContextAccessor>()`
11. `UseWolverine(...)` with `AutoApplyTransactions`, `UseDurableLocalQueues`, and `PersistMessagesWithEfCore<...>`
12. Global exception handler (custom `IExceptionHandler` → ProblemDetails with trace ID)
13. Module registrations (none yet)

### 2.2 AppHost configuration

Provision:

- Postgres with named parameter for password
- Redis
- Mailpit container for dev email
- The API project with references

### 2.3 Add baseline endpoints

- `/health`, `/health/ready`, `/health/live` (from Aspire ServiceDefaults)
- `/openapi/v1.json`
- `/scalar/v1` (dev only)

### 2.4 Verify

- `dotnet run --project src/AppHost` boots the full stack.
- Aspire dashboard shows Postgres, Redis, Mailpit, API all healthy.
- `GET /health` returns 200.
- `GET /scalar/v1` renders (empty API documentation).

**Commit.** Message: "feat: API host composition with full middleware pipeline".

---

## Phase 3: Architectural test foundation

**Goal:** The architectural test suite runs and enforces the rules we have so far.

### 3.1 Core rules

Implement in `Modulith.Architecture.Tests`:

- Shared.Kernel depends only on BCL + ErrorOr
- Shared.Kernel types have no EF Core, ASP.NET, Wolverine references
- All types have file-scoped namespaces
- DomainEvent types live in `*.Domain.Events.*` namespaces

Only a few rules right now — more added as the codebase grows.

### 3.2 Failure message quality

Each rule should produce an actionable message. Example:

```
FAIL: Modulith.Shared.Kernel.SomeType depends on Microsoft.EntityFrameworkCore.
Shared.Kernel must remain free of infrastructure dependencies.
Move EF Core-dependent types to Shared.Infrastructure.
```

### 3.3 Verify

`dotnet test tests/Modulith.Architecture.Tests` passes.

**Commit.** Message: "test: architectural test foundation".

---

## Phase 4: First module (Users, minimal)

**Goal:** A complete end-to-end slice. Proves the architecture.

### 4.1 Module projects

```bash
dotnet new classlib -n Modulith.Modules.Users -o src/Modules/Users/Modulith.Modules.Users
dotnet new classlib -n Modulith.Modules.Users.Contracts -o src/Modules/Users/Modulith.Modules.Users.Contracts
dotnet sln add src/Modules/Users/Modulith.Modules.Users src/Modules/Users/Modulith.Modules.Users.Contracts
```

### 4.2 Domain

- `UserId : TypedId<Guid>`
- `Email` value object (validation in factory method)
- `PasswordHash` value object (BCrypt)
- `User : AggregateRoot<UserId>` with `Email`, `PasswordHash`, `CreatedAt`, `DisplayName`
  - `public static Result<User> Create(...)` factory
  - `public Result ChangeEmail(Email newEmail)` — raises `UserEmailChanged` internal event
  - `public Result ChangePassword(string current, string new)` — hashes, raises event
- Domain events: `UserRegistered`, `UserEmailChanged`, `UserPasswordChanged`

### 4.3 Persistence

- `UsersDbContext : ModuleDbContext` with schema `users`
- `UserConfiguration : IEntityTypeConfiguration<User>`
- Configure value objects via conversion; strongly-typed ID via conversion
- Initial migration

### 4.4 First feature slice: Register

Six files under `Features/Register/`:

- `Register.Request.cs`: email, password, display name
- `Register.Response.cs`: user ID, access token
- `Register.Command.cs`
- `Register.Handler.cs`: validate uniqueness, hash password, save, issue JWT
- `Register.Validator.cs`: FluentValidation
- `Register.Endpoint.cs`: `POST /v1/users/register`, no auth, rate-limited with `auth` policy

### 4.5 Second feature slice: Login

Similarly structured. `POST /v1/users/login`. Issues JWT on success.

### 4.6 Third feature slice: GetCurrentUser

`GET /v1/users/me`. Authenticated. Returns current user profile.

### 4.7 Contracts

In `Users.Contracts/Events/`:
- `UserRegisteredV1`
- `UserEmailChangedV1`

### 4.8 Module registration

- `UsersOptions` with validation (JWT issuer, audience, token lifetime)
- `UsersModule.AddUsersModule`
- `UsersModule.MapUsersEndpoints`
- Wire in `Api/Program.cs`

### 4.9 Module CLAUDE.md

`src/Modules/Users/Modulith.Modules.Users/CLAUDE.md` — describes domain vocab, invariants, auth concerns.

### 4.10 Seeder

`UsersDevSeeder : IModuleSeeder` for `Development` environment. Creates two users (alice, bob).

### 4.11 Integration tests

`Modulith.Modules.Users.IntegrationTests/`:

- `UsersApiFixture`
- `RegisterTests` (happy, duplicate email, invalid email, weak password)
- `LoginTests` (happy, wrong password, unknown user)
- `GetCurrentUserTests` (happy, unauthenticated → 401)

### 4.12 Unit tests

`Modulith.Modules.Users.UnitTests/`:

- `UserTests` (create, change email, change password, invariants)
- `EmailTests` (validation)

### 4.13 Architectural tests specific to Users

- Users' `Domain/` has no EF Core references
- No public setters on `User`
- `Users.Contracts` has no reference to `Users` internal

### 4.14 Verify

- `dotnet test` all green
- `dotnet run --project src/AppHost` — Postgres container spins up, migrations run, seeder creates users
- `POST /v1/users/register` works via Scalar
- `POST /v1/users/login` returns a valid JWT
- `GET /v1/users/me` with the JWT returns the user

**Commit.** Message: "feat: Users module with register/login/me slices".

This is the longest single phase. Past this point, modules multiply similarly.

---

## Phase 5: Second module (Catalog) — prove boundaries

**Goal:** A second module exists, boundary rules hold.

### 5.1 Scaffold Catalog

Follow [`docs/how-to/add-a-module.md`](docs/how-to/add-a-module.md). A simple module — just enough to exercise boundaries.

### 5.2 Domain

- `ProductId`, `Sku` value object, `Money` value object
- `Product : AggregateRoot<ProductId>` with `Sku`, `Name`, `Price`, `IsActive`
- Simple factory, a couple of methods (`Deactivate`, `UpdatePrice`)

### 5.3 Feature slices

- `ListProducts` (GET, public)
- `CreateProduct` (POST, admin-only)
- `GetProductById` (GET)

### 5.4 Integration tests

### 5.5 Verify

- Build passes with warnings-as-errors
- All tests pass
- Architectural tests confirm:
  - Catalog doesn't reference Users' internal project
  - Catalog.Domain has no EF dependencies
  - Public events have version suffix

**Commit.** Message: "feat: Catalog module as boundary-validation proof".

---

## Phase 6: Cross-module event flow

**Goal:** Prove Wolverine's outbox delivers events between modules.

### 6.1 Add a cross-module integration point

For example: when a user is created, the Catalog module records them as a customer.

- Users publishes `UserRegisteredV1` (already exists).
- Catalog subscribes in `Integration/Subscribers/OnUserRegisteredHandler.cs`.
- Catalog has its own `Customer` read-model entity.

### 6.2 Integration test

```csharp
[Fact]
public async Task RegisteringUser_CreatesCatalogCustomer()
{
    var session = await fixture.Host.TrackActivity()
        .ExecuteAndWaitAsync(async () =>
        {
            await client.PostAsJsonAsync("/v1/users/register", ...);
        });

    session.Executed.SingleMessage<UserRegisteredV1>().ShouldNotBeNull();

    var customer = await fixture.QueryDb<CatalogDbContext>(db =>
        db.Customers.FirstOrDefaultAsync(...));
    customer.ShouldNotBeNull();
}
```

### 6.3 Verify

The integration test passes. The full cycle (HTTP → handler → outbox → cross-module handler) works.

**Commit.** Message: "feat: cross-module event flow via Wolverine outbox".

---

## Phase 7: Cross-cutting modules

**Goal:** Audit and Notifications modules, exercising the module pattern for infrastructure-heavy modules.

### 7.1 Audit module

- Own schema `audit`
- `AuditEntry` entity
- Integration handlers subscribing to events from other modules
- Query API (`GetAuditTrailQuery` in contracts)

### 7.2 Notifications module

- Own schema `notifications`
- `NotificationTemplate`, `UserNotificationPreferences`, `NotificationLog` entities
- Razor templates via `RazorLight` or equivalent
- Subscribers for events that trigger notifications (`UserRegisteredV1` → welcome email)
- `IConsentRegistry` integration
- Mailpit delivery in dev

### 7.3 Integration tests

End-to-end: register user → welcome email appears in Mailpit.

**Commit after each.**

---

## Phase 8: GDPR endpoints

**Goal:** Personal data export and erasure work end-to-end.

### 8.1 Implement per-module exporters and erasers

- Users: exporter + hard-delete eraser
- Catalog: exporter + anonymize eraser (if it holds customer data)
- Audit: anonymize-actor eraser
- Notifications: exporter + hard-delete eraser

### 8.2 Users module aggregator endpoints

- `GET /v1/users/me/personal-data`
- `DELETE /v1/users/me`

### 8.3 Consent tracking

- `Consents` table in Users
- `IConsentRegistry` implementation
- Notifications module reads consent for marketing

### 8.4 Integration tests

Full flows: export, erase, verify data is gone (or anonymized where appropriate).

**Commit.** Message: "feat: GDPR export and erasure flows".

---

## Phase 9: dotnet new templates ✓

**Goal:** `dotnet new modulith-slice` and `dotnet new modulith-module` work.

### 9.1 Slice template

`templates/slice/` with:

- `.template.config/template.json` with symbols for `--module` and `--name`
- Source files with replacement tokens

### 9.2 Module template

`templates/module/` similarly.

### 9.3 Install locally and test

```bash
dotnet new install ./templates/slice
dotnet new install ./templates/module

dotnet new modulith-module --name TestModule
dotnet new modulith-slice --module TestModule --name TestFeature
```

Verify the generated files compile and integrate.

**Commit.** Message: "feat: dotnet new item templates for slices and modules".

---

## Phase 9.5: Users module — baseline auth flows

**Goal:** Expand the Users module to the production-ready baseline — password reset, change password, email change, refresh tokens, and logout. The Users aggregate and its auth surface should be feature-complete before modeling other modules against it.

Phase 9.5 established the vertical slice shape with register/login/me. This phase proves that shape holds for a realistic set of interconnected features within a single module — tokens, rotation, revocation, cross-slice shared primitive.

Reference docs:

- [`adr/0028-auth-flows-baseline.md`](adr/0028-auth-flows-baseline.md)
- [`adr/0029-refresh-tokens-and-logout.md`](adr/0029-refresh-tokens-and-logout.md)
- [`how-to/implement-auth-flows.md`](how-to/implement-auth-flows.md)
- [`src/Modules/Users/CLAUDE.md`](../src/Modules/Users/CLAUDE.md)

---

### 9.5.1 Domain additions

Add to `Modulith.Modules.Users/Domain/`:

**`SingleUseToken` value object** (aggregate-internal entity stored in its own table):

- `TokenHash` (byte[], SHA-256)
- `Purpose` (enum: `PasswordReset`, `EmailChange`)
- `UserId`
- `IssuedAt`, `ExpiresAt`
- `ConsumedAt` (nullable)
- Factory: `Create(UserId, TokenPurpose, TimeSpan lifetime, IClock) → (SingleUseToken, string rawValue)`
- Method: `Consume(IClock)`
- Method: `IsValid(IClock) : bool`

**`RefreshToken` entity:**

- `Id` (RefreshTokenId)
- `UserId`
- `TokenHash` (byte[], SHA-256)
- `IssuedAt`, `ExpiresAt`, `RevokedAt?`, `RotatedTo?` (RefreshTokenId)
- `DeviceFingerprint?`, `CreatedFromIp?`
- Factory: `Issue(UserId, TimeSpan lifetime, IClock, string? ua, IPAddress? ip) → (RefreshToken, string rawValue)`
- Method: `Revoke(IClock)`
- Method: `MarkRotatedTo(RefreshTokenId, IClock)`
- Method: `IsActive(IClock) : bool`

**Additions to `User` aggregate:**

- `VerifyPassword(string) : bool` (constant-time via BCrypt)
- `SetPassword(string) : Result` (applies password policy, hashes, raises `PasswordChanged` internal event)
- Existing `ChangeEmail(Email) : Result` stays as-is

**`PendingEmailChange` entity:**

- `TokenHash` (byte[])
- `NewEmail` (Email)
- Expired entries swept together with the corresponding `SingleUseToken`.

**New domain events:**

- `PasswordResetRequested` (internal), maps to `PasswordResetRequestedV1` (public — includes raw token for Notifications only)
- `PasswordReset` → `PasswordResetV1`
- `PasswordChanged` → `PasswordChangedV1`
- `EmailChangeRequested` → `EmailChangeRequestedV1`
- `EmailChanged` → `EmailChangedV1`
- `UserLoggedOutAllDevices` → `UserLoggedOutAllDevicesV1`

### 9.5.2 Persistence

Add entity configurations and a migration:

```bash
dotnet ef migrations add AddTokensAndRefreshTokens \
  --project src/Modules/Users/Modulith.Modules.Users \
  --context UsersDbContext \
  --output-dir Persistence/Migrations
```

Tables added to the `users` schema:

- `user_tokens` (for `SingleUseToken`)
- `refresh_tokens`
- `pending_email_changes`

Indexes:

- `user_tokens(token_hash, purpose)` — unique
- `refresh_tokens(token_hash)` — unique
- `refresh_tokens(user_id, revoked_at)` — partial index on `revoked_at IS NULL` for active-session queries
- `refresh_tokens(expires_at)` — for sweep job

### 9.5.3 Feature slices

Add under `Features/`:

- **`ForgotPassword/`** — public, `POST /v1/users/password/forgot`
- **`ResetPassword/`** — public, `POST /v1/users/password/reset`
- **`ChangePassword/`** — authenticated, `POST /v1/users/me/password`
- **`RequestEmailChange/`** — authenticated, `POST /v1/users/me/email/request`
- **`ConfirmEmailChange/`** — authenticated, `POST /v1/users/me/email/confirm`
- **`RefreshToken/`** — public (refresh-token-authed), `POST /v1/users/token/refresh`
- **`Logout/`** — authenticated, `POST /v1/users/logout`
- **`LogoutAll/`** — authenticated, `POST /v1/users/logout/all`

Also update:

- **`Login/`** — now issues both access and refresh tokens, enforces `MaxActiveRefreshTokensPerUser`.

Each slice follows the six-file pattern (Request/Response/Command/Handler/Validator/Endpoint).

Rate-limit policies per [`adr/0018-rate-limiting.md`](adr/0018-rate-limiting.md):

| Slice | Policy |
|---|---|
| ForgotPassword, ResetPassword, ChangePassword | `auth` |
| RefreshToken, ConfirmEmailChange | `auth` |
| Logout, LogoutAll, RequestEmailChange | `write` |

### 9.5.4 Options additions

Extend `UsersOptions`:

```csharp
[Range(1, 1440)]
public int AccessTokenLifetimeMinutes { get; init; } = 15;

[Range(1, 365)]
public int RefreshTokenLifetimeDays { get; init; } = 30;

[Range(1, 100)]
public int MaxActiveRefreshTokensPerUser { get; init; } = 10;

public TimeSpan PasswordResetTokenLifetime { get; init; } = TimeSpan.FromMinutes(30);
public TimeSpan EmailChangeTokenLifetime { get; init; } = TimeSpan.FromMinutes(30);
```

### 9.5.5 Scheduled sweep job

Wolverine scheduled message:

```csharp
public sealed record SweepExpiredTokens;

internal sealed class SweepExpiredTokensHandler
{
    // Run daily: delete user_tokens and refresh_tokens where expires_at < now - 7 days
    // AND (revoked_at IS NOT NULL OR consumed_at IS NOT NULL OR expires_at < now)
}
```

Register in Wolverine as a recurring message.

### 9.5.6 Notifications module integration

The Notifications module subscribes to:

- `PasswordResetRequestedV1` → send reset email with raw token
- `PasswordResetV1` → send confirmation to old email address
- `PasswordChangedV1` → send confirmation
- `EmailChangeRequestedV1` → send confirmation to *new* address with token
- `EmailChangedV1` → send alert to *old* address

Razor templates for each. Marketing-category consent does not apply to these — they are transactional/security notifications (non-opt-out per ADR-0014).

### 9.5.7 Audit module integration

Audit subscribes to all auth-related public events:

- `UserLoggedInV1`, `UserLoggedOutAllDevicesV1`
- `PasswordResetV1`, `PasswordChangedV1`
- `EmailChangedV1`
- `UserErasureRequestedV1`

Each becomes an `AuditEntry` with `Action`, `Actor`, `EntityId`, `OccurredAt`.

### 9.5.8 Current user abstraction updates

Extend `ICurrentUser` to expose:

- `CurrentRefreshTokenId?` — the refresh token used to authenticate this request (when applicable). Populated by a middleware that maps the access token's `jti` claim (or a separate claim) back to the refresh token.

This is needed by the `ChangePassword` slice to preserve the current session while revoking others.

### 9.5.9 Security tests

Add integration tests covering:

- **Enumeration resistance**: ForgotPassword returns identical response for known vs unknown email. Response body and status code match.
- **Token anti-reuse**: using a reset token twice fails the second time with the same error as a bad token.
- **Token hashing**: DB inspection after a reset request shows no plaintext token in `user_tokens`.
- **Refresh rotation**: successful refresh revokes the old token and returns a new one.
- **Refresh reuse detection**: presenting a rotated refresh token revokes the entire chain.
- **Sensitive event revocation**: password reset, password change, and email change revoke refresh tokens per the ADR-0028 table.
- **Email change alerts old address**: an `EmailChangedV1` event is published with both old and new email.
- **MaxActiveRefreshTokensPerUser enforcement**: logging in beyond the limit revokes the oldest token.
- **Concurrent refresh**: two simultaneous requests with the same refresh token — one succeeds, one fails (or both succeed with the second's result superseding, depending on impl; pick and document).

### 9.5.10 Unit tests

Add domain tests for:

- `SingleUseToken.Create` returns token whose hash matches `SHA256(rawValue)`.
- `SingleUseToken.IsValid` behavior across consumed / expired / active states.
- `RefreshToken.Issue` likewise.
- `RefreshToken.MarkRotatedTo` sets both `RevokedAt` and `RotatedTo`.
- `User.SetPassword` rejects via password policy when too short.
- `User.VerifyPassword` is constant-time (hard to unit-test directly; structural test that it calls `BCrypt.Verify`).

### 9.5.11 Architectural tests

Add or extend:

- `SingleUseToken` has no public setters (it's an aggregate-internal entity).
- `RefreshToken` has no public setters.
- No type in Users module stores raw tokens (checked via static analysis of string column names — heuristic).
- Forbid `DateTime.UtcNow` in the module; require `IClock` (general arch rule, this phase adds reliance on it).

### 9.5.12 Update module CLAUDE.md

Replace the Phase 4 stub `src/Modules/Users/CLAUDE.md` with the version shipped in `src/Modules/Users/CLAUDE.md` (this addendum's companion file). It documents:

- Current scope (all flows now implemented)
- Security invariants
- Explicit extension points (2FA, email confirmation gating, account lockout, breach password check, external IdPs, multi-tenancy, admin impersonation)
- Footguns specific to this module

### 9.5.13 Verify

- All tests pass (unit, architectural, integration).
- `dotnet run --project src/AppHost` — stack comes up, seeded users can register → login → rotate tokens → change password → reset password via email captured in Mailpit → log out everywhere.
- Manual walk-through of the full lifecycle via Scalar UI.
- Review logs to confirm no raw tokens are emitted.

**Commit.** Message: "feat(users): password reset, change, email change, refresh tokens, and logout".

---

## Already-built modules may need small additions

- Notifications: add subscribers + templates for the new events
- Audit: add subscribers for new events (automatic if audit has a generic subscription pattern)
- Phase 9 scaffolders: verify they produce the correct shape for the new slices (especially validator-on-Request)

---

## What this phase does NOT include

The extension points in `src/Modules/Users/CLAUDE.md` are deliberate non-goals:

- Two-factor authentication
- Email confirmation as an access gate
- Account lockout
- Breach password check
- External identity providers (OIDC federation)
- Multi-tenancy / organization membership
- Admin impersonation

These are extension points documented with suggested shapes but not implemented. Adding any of them is a separate, deliberate decision.

---

## Phase 10: Smoke tests + CI

**Goal:** Full stack tested, CI wired.

### 10.1 Smoke tests

3-5 tests in `Modulith.SmokeTests`:

- Stack boots successfully
- Register + login + get-me works through real pipeline
- Notification arrives in Mailpit
- OpenAPI document generates with expected structure

### 10.2 CI pipeline

`.github/workflows/ci.yml` (or equivalent):

- Fast tier: `dotnet build` + `dotnet test --filter "Category!=Integration&Category!=Smoke"`
- PR tier: + integration tests
- Release tier: + smoke tests

Docker-in-Docker or Docker service for Testcontainers.

### 10.3 Verify

CI runs green on a sample PR.

**Commit.** Message: "ci: three-tier test pipeline".

---

## Phase 11: Documentation refinement

**Goal:** Docs match the implementation.

### 11.1 Update how-to guides

Reference real files, real commands, real paths. Remove "once implemented" placeholders.

### 11.2 Add `docs/examples/`

Worked slices copied out of real modules. Pick 3-5 patterns:

- Simple CRUD slice
- Cross-module event publish/subscribe
- File upload via IBlobStore
- Scheduled job
- Notification-triggering event

### 11.3 Per-module CLAUDE.md files

Each module gets its specific operating manual now that the code exists.

### 11.4 README update

Remove "documentation-complete, implementation pending" — update to status and real quickstart.

**Commit.** Message: "docs: align with implementation".

---

## Phase 12: Polish

### 12.1 Add architectural tests for everything covered in ADR-0015

By now, most rules are already in place. Audit the full list and add any missing.

### 12.2 Health check detail

Per-module health checks registered in `AddXxxModule`. Dashboard shows them.

### 12.3 Observability polish

Custom activity sources per module for key operations. Metrics for domain events published, handled, failed.

### 12.4 Sample data expansion

Seeders produce richer, more interconnected dev data.

### 12.5 Error catalog consolidation

Audit all `Errors.Xxx` classes. Ensure codes are stable and documented.

**Commit.** Message: "chore: polish and observability improvements".

---

## Phase 13: RBAC, Bearer in OpenAPI

**Goal:** Promote authorization from "authenticated / not" to role-based with permission-level policy checks, and make the Scalar UI able to send `Authorization: Bearer <token>` against protected endpoints.

Reference docs:

- [`adr/0030-rbac.md`](adr/0030-rbac.md)
- [`adr/0028-auth-flows-baseline.md`](adr/0028-auth-flows-baseline.md)
- [`adr/0029-refresh-tokens-and-logout.md`](adr/0029-refresh-tokens-and-logout.md)

Design summary (full reasoning in ADR-0030):

- **One role per user.** Roles cannot be granted or revoked; only *changed*. Simplifies invariants, claim surface, and audit.
- **Roles → permissions**, permissions are dotted strings declared as constants by each module in its `.Contracts` project and aggregated at startup.
- **Endpoints authorize on permissions**, not roles. Policies are registered at startup, one per declared permission.
- **JWT carries only the `role` claim.** Permissions are resolved per request from an in-memory `role → permission[]` map via `IClaimsTransformation`. Keeps tokens small and permissions always current relative to code.
- **Role change forces re-login** via refresh-token revocation. There is no "stale role in token" window beyond the access-token TTL.
- **Seeded roles**: `Admin` (all declared permissions) and `User` (no elevated permissions). More roles can be defined later without schema changes.

---

### 13.1 Users module — domain additions

Add to `Modulith.Modules.Users/Domain/`:

**`Role` value object (enum-like):**

- Reference-by-name (case-sensitive, ASCII, `^[a-z][a-z0-9_-]{1,31}$`).
- Two seeded instances exposed as static members: `Role.Admin`, `Role.User`.
- Comparable and `IEquatable` by name.

**Addition to `User` aggregate:**

- `Role` property, non-nullable, private setter.
- Factory `User.Create(...)` now takes an initial `Role` parameter (default `Role.User`) and asserts it is non-null.
- Method `User.ChangeRole(Role newRole, UserId changedBy) : Result` — no-op and returns failure if `newRole == Role`; otherwise sets and raises `UserRoleChanged`.

**New domain event:**

- `UserRoleChanged` (internal) — `UserId`, `OldRole`, `NewRole`, `ChangedBy`.
- Maps to `UserRoleChangedV1` (public contract) — same fields, intended for Audit and Notifications subscribers.

### 13.2 Users module — persistence

- Add a `role` column (`text`, non-null) to the `users` table with default `'user'`.
- Migration `AddUserRole`: backfills existing rows to `'user'`, then adds the `NOT NULL` constraint.
- No new tables. Role→permission mapping is in code, not the database (see ADR-0030 for the trade-off).

```bash
dotnet ef migrations add AddUserRole \
  --project src/Modules/Users/Modulith.Modules.Users \
  --context UsersDbContext \
  --output-dir Persistence/Migrations
```

### 13.3 Permission declarations — per module

Each module declares its permission constants in its `.Contracts` project under `Authorization/`:

```csharp
// Modulith.Modules.Catalog.Contracts/Authorization/CatalogPermissions.cs
public static class CatalogPermissions
{
    public const string ProductsRead  = "catalog.products.read";
    public const string ProductsWrite = "catalog.products.write";

    public static IReadOnlyCollection<string> All { get; } =
        [ProductsRead, ProductsWrite];
}
```

Apply to:

- `Modulith.Modules.Users.Contracts` → `UsersPermissions` (role management, user administration).
- `Modulith.Modules.Catalog.Contracts` → `CatalogPermissions`.
- `Modulith.Modules.Audit.Contracts` → `AuditPermissions` (read trail).
- `Modulith.Modules.Notifications.Contracts` → `NotificationsPermissions` (template admin).

Format rule (enforced by architectural test, 13.10): `{module}.{resource}.{action}`, lowercase, dot-separated, ASCII.

### 13.4 Users module — authorization composition

Add `Security/Authorization/`:

- **`IPermissionCatalog`** — resolves `role → IReadOnlyCollection<string>` and exposes the set of all declared permissions. Populated at startup from the contracts.
- **`RolePermissionMap`** — static map. `Admin` → all declared permissions; `User` → empty (endpoints that only need authentication still use the existing `Authenticated` policy).
- **`PermissionClaimsTransformation : IClaimsTransformation`** — reads the `role` claim from the incoming principal, looks up the permissions from `IPermissionCatalog`, and adds them as `permission` claims to a cloned `ClaimsIdentity`. Idempotent (skips if already present).
- **`PermissionRequirement` + `PermissionAuthorizationHandler`** — matches a `permission` claim.
- **`AuthorizationBuilderExtensions.AddPermissionPolicies(...)`** — registers one named policy per declared permission so endpoints can call `.RequireAuthorization(CatalogPermissions.ProductsWrite)`.

These live in the Users module (it owns identity) and are wired into the API host via a new `AddRbac(...)` extension on `UsersModule`:

```csharp
services.AddRbac(); // registers catalog, claims transformation, auth handler, policies
```

The catalog is assembled by discovering `public static IReadOnlyCollection<string> All` on `*Permissions` types across the loaded contracts assemblies.

### 13.5 JWT issuance — role claim

Update `JwtGenerator`:

- Add a `role` claim (`ClaimTypes.Role`) carrying the user's current role name.
- No permission claims in the token. The claims transformation injects them per request.

### 13.6 `ICurrentUser` extension

Extend the shared kernel interface:

```csharp
string? Role { get; }
IReadOnlyCollection<string> Permissions { get; }
bool HasPermission(string permission);
```

Update `CurrentUser` default to read `ClaimTypes.Role` and all `permission` claims. This is a breaking change to `Modulith.Shared.Kernel`; all call sites already in-tree update trivially.

### 13.6a `GET /v1/users/me` — include role and permissions

Update `GetCurrentUserResponse` to carry the authorization surface the frontend needs for show/hide gating. The JWT still only carries `role` (per 13.5); `me` is the canonical hydration point for the client after login or refresh.

New fields on the response:

- `role: string` — the user's current role name (e.g. `"admin"`, `"user"`).
- `permissions: string[]` — the fully resolved permission set for that role, sorted lexicographically. Always present; empty array for roles with no permissions.
- `permissionsVersion: string` — short, stable hash (e.g. SHA-256 of the sorted permissions joined by `\n`, base64url, first 16 chars) so the frontend can detect changes cheaply.

Source of data: `IPermissionCatalog` from 13.4. The handler does not query the database for permissions — it reads the caller's role from the loaded `User` and resolves the permission set from the in-memory map. `permissionsVersion` is computed at startup per role and cached; the response just reads it.

Contract notes:

- Ordering is part of the contract. Frontends may compare by value, not by set.
- `permissions` is never `null`.
- When a role change occurs, refresh tokens are revoked (per 13.7); the frontend's next `me` after re-login returns the new set and a new `permissionsVersion`.

Response caching: add `Cache-Control: private, no-store` on the endpoint. `me` is authenticated and user-specific; shared caches must not store it.

### 13.7 Feature slices — role management

Add under `Modules/Users/Modulith.Modules.Users/Features/`:

- **`ChangeUserRole/`** — `PUT /v1/users/{userId}/role`
  - Body: `{ "role": "admin" | "user" }`.
  - Authorization: `UsersPermissions.RolesWrite` (Admin only by default).
  - Prevents an admin from changing their own role (avoids the last-admin footgun).
  - On success: updates role, revokes all refresh tokens for the target user, publishes `UserRoleChangedV1`.
  - Rate limit: `write`.
- **`ListUsers/`** — `GET /v1/users`
  - Authorization: `UsersPermissions.UsersRead`.
  - Paginated; returns id, email, display name, role.
- **`GetUserById/`** — `GET /v1/users/{userId}`
  - Authorization: `UsersPermissions.UsersRead`.

All six files per slice (Request/Response/Command/Handler/Validator/Endpoint) per the standard pattern.

Update **existing endpoints** to migrate off the plain `Authenticated` policy where a permission now applies:

- `CatalogPermissions.ProductsWrite` → `CreateProduct` endpoint.
- `AuditPermissions.TrailRead` → `GetAuditTrail` endpoint (previously authenticated-only; this is a privilege escalation fix).
- Notifications admin endpoints (if any) → `NotificationsPermissions.*`.

Endpoints tied to `me` (profile, password change, account deletion, personal-data export) stay on `Authenticated` — they don't need permission scoping.

### 13.8 Admin bootstrap

Two mechanisms — development and production — live side-by-side:

- **Dev seeder.** Extend `UsersDevSeeder` to ensure one admin exists. Email + display name from `Modules:Users:Dev:AdminEmail` / `AdminDisplayName` configuration. Idempotent.
- **Non-dev bootstrap.** New `AdminBootstrapper : IHostedService` registered only when `Modules:Users:AdminBootstrap:Enabled` is `true`. On startup:
  - If at least one user with role `Admin` exists → no-op, log at Information.
  - Else: read `Modules:Users:AdminBootstrap:Email`. If missing → fail fast with a clear error (refuse to start without an admin).
  - If a user with that email exists → promote to `Admin` (same code path as `ChangeUserRole`), emit `UserRoleChangedV1`.
  - Otherwise → no-op and log a Warning. Creating a user from nothing during bootstrap is out of scope (no password, no email confirmation). Operators register the user via the normal flow, then bootstrap promotes them.
  - The hosted service runs once per process start. Safe under rolling deployments.

Document both in `docs/how-to/auth/bootstrap-admin.md`.

### 13.9 OpenAPI — Bearer security scheme

Add `src/Api/Infrastructure/OpenApi/`:

- **`BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer`** — adds a `Bearer` security scheme (type `http`, scheme `bearer`, bearer format `JWT`) to the document's components, merging with any existing schemes rather than replacing the whole map. Safe to use alongside other document transformers that register their own schemes.
- **`AuthorizationOperationTransformer : IOpenApiOperationTransformer`** — for each operation, inspect the endpoint's metadata for `IAuthorizeData`. If present, attach a `Bearer` security requirement. Anonymous endpoints remain requirement-free.

Wire in `Program.cs`:

```csharp
builder.Services.AddOpenApi(opts =>
{
    opts.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    opts.AddOperationTransformer<AuthorizationOperationTransformer>();
});
```

Verify: Scalar UI shows an "Authorize" button; protected operations display a lock; anonymous operations do not.

### 13.10 Architectural tests

Add to `Modulith.Architecture.Tests`:

- **Permission naming.** All `const string` fields on any `*Permissions` type match `^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*){2}$` (three dot-separated segments).
- **Permission ownership.** The first segment of a permission constant equals the owning module's short name (`catalog.*` lives in `Modulith.Modules.Catalog.Contracts`).
- **No role literals in endpoints.** Endpoints do not call `RequireAuthorization("admin")` or `RequireRole(...)`; authorization is permission-based. (String literal check over endpoint files.)
- **No raw role claim reads outside `CurrentUser`.** `ClaimTypes.Role` string is only referenced inside `Modulith.Shared.Infrastructure.Identity.CurrentUser` and `Modulith.Modules.Users.Security.*`.
- **`User.Role` has a private setter.**

### 13.11 Integration tests

Add to Users integration tests:

- **Role change round-trip.** Admin → PUT role → target's refresh tokens revoked; target's next request with the *old* access token still works (until expiry); target's next refresh with the old refresh token fails; after re-login the new role is reflected in `GET /v1/users/me`.
- **Self-role-change forbidden.** Admin changing their own role returns 400 with a specific error code.
- **Permission gating.** Non-admin GET `/v1/users` → 403; admin → 200.
- **Claims transformation.** Request with role `admin` token results in `permission` claims present on `HttpContext.User` (verified via a test-only diagnostic endpoint, or by behavior: endpoint requiring `catalog.products.write` succeeds for admin, fails for user).
- **`me` exposes role and permissions.** `GET /v1/users/me` for an admin returns `role: "admin"`, the full permission list sorted lexicographically, and a non-empty `permissionsVersion`. For a `user`-role caller, `permissions` is `[]` and `permissionsVersion` reflects the empty set. `permissionsVersion` is stable across requests for the same role within a process and changes when the role→permission map changes.
- **Audit subscription.** `UserRoleChangedV1` produces an `AuditEntry` with old/new role and actor.

Add to Catalog integration tests:

- Admin can create a product; user cannot (403).

Add OpenAPI smoke test:

- `GET /openapi/v1.json` includes a `Bearer` security scheme.
- A protected operation (`POST /v1/catalog/products`) has a `security` requirement on it.
- A public operation (`POST /v1/users/login`) has no `security` requirement.

### 13.12 Unit tests

- `User.ChangeRole` rejects same role, accepts different role, raises `UserRoleChanged`.
- `Role` value-object equality and validation.
- `PermissionClaimsTransformation` is idempotent.
- `PermissionAuthorizationHandler` succeeds when the permission claim is present and fails otherwise.

### 13.13 GDPR export

Update `UsersPersonalDataExporter` to include the user's current role under a `Role` field. No change to the eraser — role is not sensitive and is deleted naturally with the row.

### 13.14 Notifications integration (optional but recommended)

Subscribe Notifications to `UserRoleChangedV1` and send a transactional "your account role has changed" email (non-opt-out, per ADR-0014). Old role + new role in the body; no tokens. Skip if shipping the minimum.

### 13.15 Audit integration

Add `UserRoleChangedV1` to the Audit module's subscribed-events list. Standard pattern; no new shape needed.

### 13.16 Documentation

- New ADR: **`docs/adr/0030-rbac.md`** — roles, permissions, claim-delivery strategy, bootstrap, trade-offs.
- New how-to: **`docs/how-to/auth/use-rbac.md`** — declaring a permission, applying it to an endpoint, testing it.
- New how-to: **`docs/how-to/auth/bootstrap-admin.md`** — dev seeder + non-dev hosted-service bootstrap.
- Update `docs/how-to/add-a-slice.md` to reference the permission convention.
- Update `src/Modules/Users/CLAUDE.md` — RBAC is no longer an extension point; document role invariants.
- Update `src/Modules/Catalog/CLAUDE.md` (and other modules' CLAUDE.md) — reference their own `*Permissions` contract.

### 13.17 Configuration additions

`Modulith.Api/appsettings.json` and dev overrides:

```json
"Modules": {
  "Users": {
    "Dev": {
      "AdminEmail": "admin@example.test",
      "AdminDisplayName": "Admin"
    },
    "AdminBootstrap": {
      "Enabled": false,
      "Email": null
    }
  }
}
```

`AdminBootstrap` is `Enabled: false` by default. Operators turn it on explicitly in non-dev environments.

### 13.18 Verify

- `dotnet test` all green.
- `dotnet run --project src/AppHost` — seeded admin can log in, change another user's role, and see the old user's sessions revoked.
- Scalar at `/scalar/v1` — "Authorize" button accepts a Bearer token; protected endpoints call through; anonymous endpoints remain callable without a token.
- OpenAPI document contains `securitySchemes.Bearer` and per-operation security requirements only where `[Authorize]`/`RequireAuthorization` is applied.
- No role literals in endpoint code (architectural test green).

**Commit.** Message: "feat: RBAC with per-module permissions and Bearer in OpenAPI".

---

## What "done" looks like

At the end of this roadmap:

- `git clone` + `dotnet run --project src/AppHost` produces a running, healthy stack.
- All tests pass: unit, architectural, integration, smoke.
- Scalar at `/scalar/v1` shows versioned, documented endpoints.
- Register → login → do things → erase account all work end-to-end.
- Cross-module event flow observable in the Aspire dashboard (traces connect).
- `dotnet new modulith-module` and `dotnet new modulith-slice` produce correct scaffolds.
- CI runs three tiers reliably.
- Documentation matches code.

This is the template. Further work (new modules, richer features) happens on top, following the patterns now established.

---

## Tips for Claude Code working through this

1. **Read the relevant ADR(s) before each phase.** They contain reasoning the roadmap doesn't repeat.
2. **Commit at the end of each phase.** Small atomic commits > big ones.
3. **Run the architectural tests early and often.** They catch mistakes at the fastest feedback loop.
4. **When in doubt, ask.** The roadmap is a sequence, not a silencer for uncertainty.
5. **Don't skip integration tests.** The fast tier doesn't validate end-to-end. Integration tests are where the architecture is proven.
6. **Keep the agent capability boundaries (in `CLAUDE.md`) in mind.** Don't modify `Directory.Packages.props`, composition root, or similar without explicit instruction.
7. **Package choices are deliberate.** If a package seems missing, ask before adding one.
8. **Follow the scoped `CLAUDE.md` files.** Each directory's manual is more specific than the root.

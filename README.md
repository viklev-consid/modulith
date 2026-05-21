# Modulith

A production-grade modular monolith template for building RESTful APIs in .NET 10 / C# 14, orchestrated with .NET Aspire 13.x.

Modulith is opinionated. It encodes a set of decisions that have been made deliberately, with trade-offs documented. It is designed to be equally ergonomic for humans and for AI coding agents.

## Why a modular monolith

A modular monolith is the pragmatic middle ground between a traditional monolith and microservices. You keep the operational simplicity of one deployable unit, one runtime, and one local development environment, while still enforcing real boundaries around business capabilities.

Those boundaries matter even if you never extract a service. Each module owns its own domain, persistence, endpoints, and public contracts, so changes can stay local to the capability they belong to instead of leaking into the whole codebase. That gives you room to make architectural decisions at the module boundary rather than turning every design choice into an application-wide one. In this template, that plays out as vertical slices: each module is a vertical slice of business capability, and features inside the module are organized as vertical slices because that optimizes for the most common task in a growing system: changing a feature.

The trade-off is real. One host still means one bad deployment can affect everything, and scaling stays vertical until a module is extracted. A modular monolith without enforced boundaries is also just a monolith with better folder names. That is why this template treats contracts, per-module schemas, and architectural tests as non-negotiable.

Those same properties also make the codebase easier for AI agents to work in. Clear module ownership reduces ambiguity about where code belongs, vertical slices reduce the amount of code needed to understand a feature, and architectural tests turn boundary mistakes into fast feedback. See [Working with AI agents](#working-with-ai-agents) below for the agent-specific tooling this template adds.

## What you get

- **Vertical slice architecture** with per-feature folders
- **Modular boundaries** enforced at compile time and by architectural tests
- **Wolverine** for in-process messaging, transactional outbox, delayed messages, and handler middleware
- **TickerQ** for recurring scheduled jobs with an admin dashboard at `/admin/jobs`
- **EF Core per module** with its own schema and migrations
- **Rich domain model** with invariants enforced via factory methods and private setters
- **Result pattern** for expected failures, exceptions reserved for truly exceptional cases
- **ProblemDetails** for all error responses via `IExceptionHandler`
- **JWT bearer authentication** with a lightweight Users module (no ASP.NET Identity) — register, invite-only or disabled registration modes, login, optional TOTP two-factor authentication, password reset, change password, email change with confirmation, refresh token rotation, logout, and logout-everywhere
- **Role-based access control (RBAC)** — `admin` / `user` roles, `PermissionCatalog` auto-discovers all `*Permissions` types from `*.Contracts` assemblies at startup, per-permission `AuthorizationPolicy` instances registered automatically, `PermissionClaimsTransformation` adds permission claims per request from the JWT role claim
- **FluentValidation** for request validation
- **Scalar** for OpenAPI documentation
- **API versioning** via `Asp.Versioning`
- **Serilog** wired to OpenTelemetry, config-driven
- **HybridCache** with Redis via Aspire
- **Built-in rate limiting** with tiered policies
- **Microsoft.FeatureManagement** for feature flags
- **Blob storage abstraction** with a local-disk reference implementation
- **Notifications module** with transactional email, product-facing bell notifications, SSE updates, user preferences, retention cleanup, Razor templates, and Mailpit for dev
- **Audit module** consuming domain events for change history
- **Per-module health checks** registered under the `ready` tag, visible in the Aspire dashboard
- **Per-module OpenTelemetry activity sources** with full handler instrumentation
- **GDPR and legal primitives**: data classification attributes, exporter/eraser contracts, consent tracking, backend-owned legal Markdown, audited Terms/Privacy acceptance, and continued-use re-acceptance gates
- **Testing**: xUnit v3, Shouldly, Verify, Bogus, Testcontainers, NetArchTest, WireMock.Net
- **Agent-ready**: layered agent guidance (`AGENTS.md` + `CLAUDE.md`), comprehensive ADRs, `dotnet new` item templates, and repo-local automation/skills for common workflows

## Quick start

### Prerequisites

- .NET 10 SDK
- Docker Desktop, Colima, Podman, or another local container runtime supported by Aspire
- HTTPS development certificates:

```bash
dotnet dev-certs https --trust
```

### 1. Clone and build

```bash
git clone <your-fork> my-api
cd my-api

dotnet restore
dotnet build
```

### 2. Configure local development secrets

The API validates configuration at startup. For a fresh clone, set these values in the API project's user-secrets store:

```bash
dotnet user-secrets set "Jwt:SigningKey" "local-dev-signing-key-change-me-32chars-minimum" --project src/Api
```

Notes:

- `Jwt:SigningKey` must be at least 32 characters.
- The default dev admin is configured in `src/Api/appsettings.Development.json`. Override it with user-secrets only if you want a different seeded admin:

```bash
dotnet user-secrets set "Modules:Users:Dev:AdminEmail" "admin@example.test" --project src/Api
dotnet user-secrets set "Modules:Users:Dev:AdminDisplayName" "Admin" --project src/Api
```

### 3. Run the full local stack

Start the Aspire app host:

```bash
dotnet run --project src/AppHost
```

On the first run, Aspire may prompt for the `db-password` parameter. Choose any strong local password; Aspire stores it in local user secrets for the AppHost.

The AppHost starts:

- Postgres
- pgAdmin
- Redis
- Mailpit
- the migration service
- the API

Open the Aspire dashboard from the URL printed in the terminal. From there, use the `api` resource endpoint and append `/scalar/v1` to open Scalar API docs. When running the API directly from its launch profile, Scalar is usually available at:

```text
http://localhost:5125/scalar/v1
```

Mailpit is linked from the Aspire dashboard. Use it to inspect development emails such as password reset, email change, welcome, and 2FA security notifications.

### 4. Sign in with seeded users

Development seeders run automatically in `Development` unless disabled with `Modules:Seeders:Enabled=false`.

Seeded admin:

| Email | Password | Role |
|---|---|---|
| `admin@example.test` | `Admin1!Admin1!` | `admin` |

Seeded regular users:

| Email | Password | Role |
|---|---|---|
| `alice@example.com` | `Password1!` | `user` |
| `bob@example.com` | `Password1!` | `user` |
| `charlie@example.com` | `Password1!` | `user` |
| `diana@example.com` | `Password1!` | `user` |
| `eve@example.com` | `Password1!` | `user` |
| `frank@example.com` | `Password1!` | `user` |
| `grace@example.com` | `Password1!` | `user` |
| `henry@example.com` | `Password1!` | `user` |

To authorize in Scalar:

1. Call `POST /v1/users/login` with:

```json
{
  "email": "admin@example.test",
  "password": "Admin1!Admin1!"
}
```

2. Copy `session.accessToken` from the response.
3. Click **Authorize** in Scalar.
4. Enter the token as a bearer token.

Seeded users do not have two-factor authentication enabled. If you enable 2FA for a seeded account, later logins return `status: "twoFactorRequired"` with a challenge instead of a session until you complete `POST /v1/users/login/2fa`.

### 5. Run tests

```bash
dotnet test
```

Useful faster checks while developing:

```bash
dotnet test --filter "Category!=Integration&Category!=Smoke"
dotnet test tests/Modules/Users/Modulith.Modules.Users.IntegrationTests
```

## Common configuration

Most module behavior is configured under `Modules:<ModuleName>`. The Users module defaults to open registration, but templates can switch account creation to invite-only or disable registration entirely:

```json
{
  "Modules": {
    "Users": {
      "Registration": {
        "Mode": "Open",
        "InvitationTokenLifetime": "7.00:00:00"
      }
    }
  }
}
```

Supported `Modules:Users:Registration:Mode` values:

- `Open` — anyone can register, and first-time external login can provision an account.
- `InviteOnly` — password registration and first-time external-login provisioning require a valid invitation token.
- `Disabled` — new account registration/provisioning is closed; existing users can still log in.

The Notifications module separates account/security email from product-facing bell notifications. Password reset, password changed, email change, welcome, and external-login notifications stay email-first. Bell notifications are for in-app product activity such as replies, mentions, assignments, approvals, and workflow updates. The bell API is scoped to the current user under `/v1/me/notifications`, with unread counts, read/archive actions, SSE live updates, preferences under `/v1/me/notification-preferences`, and scheduled retention cleanup.

The Users module owns legal document content and acceptance state. Terms of Service and Privacy Policy copy lives as backend Markdown under `src/Modules/Users/Modulith.Modules.Users/LegalDocuments/`, is seeded into the Users database from `Modules:Users:TermsOfServiceVersion` and `Modules:Users:PrivacyPolicyVersion`, and is served to clients for onboarding and re-acceptance flows. Clients must echo document ID, version, and content hash when accepting; the backend records immutable acceptances and can return HTTP 428 `ProblemDetails` when a blocking current document is missing. See [Manage legal documents](docs/how-to/auth/manage-legal-documents.md).

## Working with AI agents

Modulith treats AI coding agents as first-class collaborators alongside human developers. The structural choices that make the codebase navigable for humans — vertical slices, explicit module boundaries, ADRs, exhaustive architectural tests — are the same choices that make it safe for agents to operate in autonomously. Neither audience is an afterthought.

### Layered guidance

Agent context is organized hierarchically so the right instructions are always close to the work:

- **`/AGENTS.md`** — primary repo-wide operating manual: invariants, workflow, common commands, footguns
- **Scoped `AGENTS.md` files** — when present, they refine guidance for specific subtrees
- **`/CLAUDE.md` + scoped `CLAUDE.md` files** — compatibility guidance for Claude-oriented workflows

### Agent tooling in this repo

This repository includes agent tooling designed to keep implementation aligned with architecture rules. Some of it is generic and some of it is agent-specific.

The `.claude/` directory ships an active harness for Claude Code, plus repo-local skills for recurring implementation workflows. Codex and other agents should use `AGENTS.md` (and scoped variants) as the primary instruction surface.

**Hooks** enforce rules at the moment they would be violated, not at PR review time:

| Hook | Fires | Does |
|---|---|---|
| `session-context.sh` | Session start | Injects project overview, git state, and module list |
| `guard-paths.sh` | Before every edit | Blocks ADR writes, production config edits, domain infrastructure imports, and cross-module references that bypass Contracts |
| `post-edit-dotnet.sh` | After every `.cs` edit | Runs format check, build, and arch tests on the touched project; failures surface in the conversation immediately |
| `slice-completeness.sh` | After every `.cs` edit | Reports missing slice files when a feature folder is incomplete |
| `stop-format.sh` | On session stop | Auto-fixes formatting on changed projects |
| `stop-gate.sh` | On session stop | Blocks the session ending if architecture tests are failing |

**Slash commands** provide scoped, tool-constrained workflows:

- `/check` — format, build, arch tests, and unit tests on projects changed in this session
- `/new-slice <Module> <Feature>` — scaffolds a vertical slice via `dotnet new modulith-slice`
- `/new-module <Name>` — scaffolds a new module with an explicit confirmation step
- `/new-adr <title>` — drafts an ADR in chat; never writes the file

**Skills** provide reusable, task-specific guidance that complements the hooks and commands. They live under `.claude/skills/` and cover the repo's most common implementation surfaces:

- `vertical-slice` — canonical walkthrough for adding or modifying a slice end-to-end
- `module-boundary` — cross-module communication rules and event/query/command decisions
- `rich-domain-model` — aggregates, value objects, typed IDs, and internal domain events
- `testing-strategy` — choosing the correct test layer and using the shared test harness well
- `ef-migration` — per-module migration workflow and destructive-change safety
- `wolverine-messaging` — `IMessageBus`, outbox semantics, subscribers, and delayed messages
- `gdpr-primitives` — personal-data classification, export/erasure hooks, consent, and retention
- `access-control` — endpoint-level authorization, `ICurrentUser`, and resource policies
- `authorization-model` — the system RBAC model: roles, permissions, registration, and claim expansion
- `auth-flows` — Users-module authentication flows and token-security invariants

**Sub-agents** handle focused tasks with tightly scoped tool access:

- `domain-modeler` — works exclusively in `Domain/` folders; writes unit tests alongside models
- `integration-tester` — writes integration tests with Testcontainers, Wolverine TrackActivity, and WireMock.Net
- `migration-writer` — adds EF Core migrations and summarizes the generated SQL
- `adr-drafter` — turns design conversations into ADR drafts for human review and commit

### Supported agent workflows

- **Codex**: uses `AGENTS.md` and any scoped `AGENTS.md` files as the primary operating instructions.
- **Claude Code**: uses layered `CLAUDE.md` files and the `.claude/` harness (hooks, commands, skills, sub-agents).
- **Other agents**: can follow the same architecture docs, ADRs, and boundary rules, with `AGENTS.md` as the baseline guidance.

### Why both humans and agents benefit

The structural choices reinforce each other:

| Mechanism | Human benefit | Agent benefit |
|---|---|---|
| Vertical slices + co-location | Navigate by feature, not by layer | Unambiguous file placement |
| Arch tests with readable failure messages | Catches regressions in CI | Self-correcting feedback mid-session |
| ADRs for every decision | Onboarding and institutional memory | Answers "why?" without guessing |
| `dotnet new` templates | Fast, consistent scaffolding | Correct namespaces without inference |
| Hooks + path guards | Backstop for code review | Enforced before code is written |
| Result pattern + no exceptions | Readable call sites | Predictable handler contracts |

> See [`docs/agentic-development.md`](docs/agentic-development.md) for the full breakdown of agent mechanisms, capability boundaries, and the recommended reading order for humans and agents new to the codebase.

## Documentation layout

- [`docs/architecture.md`](docs/architecture.md) — the big picture and request flow
- [`docs/glossary.md`](docs/glossary.md) — terms and definitions as used in this codebase
- [`docs/testing-strategy.md`](docs/testing-strategy.md) — how tests are organized and what each layer covers
- [`docs/agentic-development.md`](docs/agentic-development.md) — how this template is set up for AI agent development
- [`docs/adr/`](docs/adr/) — Architecture Decision Records explaining *why* each major decision was made
- [`docs/how-to/`](docs/how-to/) — practical guides for common tasks
- [`docs/how-to/add-scheduled-job.md`](docs/how-to/add-scheduled-job.md) — adding recurring TickerQ jobs that dispatch Wolverine commands
- [`docs/examples/`](docs/examples/) — worked patterns extracted from real modules (query slice, command+event, cross-module subscriber, scheduled job, security-sensitive slice)
- [`COMPLIANCE.md`](COMPLIANCE.md) — GDPR posture and compliance considerations
- [`CONFIG.md`](CONFIG.md) — configuration hierarchy and secrets management
- [`AGENTS.md`](AGENTS.md) — primary operating manual for AI agents working in this codebase
- [`CLAUDE.md`](CLAUDE.md) — Claude-oriented compatibility and tooling guide

## License

MIT — see [LICENSE](LICENSE).

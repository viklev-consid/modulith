# Modulith

A production-grade modular monolith template for building RESTful APIs in .NET 10 / C# 14, orchestrated with .NET Aspire 13.x.

Modulith is opinionated. It encodes a set of decisions that have been made deliberately, with trade-offs documented. It is designed to be equally ergonomic for humans and for AI coding agents.

## What you get

- **Vertical slice architecture** with per-feature folders
- **Modular boundaries** enforced at compile time and by architectural tests
- **Wolverine** for in-process messaging, transactional outbox, and background jobs
- **EF Core per module** with its own schema and migrations
- **Rich domain model** with invariants enforced via factory methods and private setters
- **Result pattern** for expected failures, exceptions reserved for truly exceptional cases
- **ProblemDetails** for all error responses via `IExceptionHandler`
- **JWT bearer authentication** with a lightweight Users module (no ASP.NET Identity) — register, login, password reset, change password, email change with confirmation, refresh token rotation, logout, and logout-everywhere
- **Role-based access control (RBAC)** — `admin` / `user` roles, `PermissionCatalog` auto-discovers all `*Permissions` types from `*.Contracts` assemblies at startup, per-permission `AuthorizationPolicy` instances registered automatically, `PermissionClaimsTransformation` adds permission claims per request from the JWT role claim
- **FluentValidation** for request validation
- **Scalar** for OpenAPI documentation
- **API versioning** via `Asp.Versioning`
- **Serilog** wired to OpenTelemetry, config-driven
- **HybridCache** with Redis via Aspire
- **Built-in rate limiting** with tiered policies
- **Microsoft.FeatureManagement** for feature flags
- **Blob storage abstraction** with a local-disk reference implementation
- **Notifications module** with Razor templates and Mailpit for dev
- **Audit module** consuming domain events for change history
- **Per-module health checks** registered under the `ready` tag, visible in the Aspire dashboard
- **Per-module OpenTelemetry activity sources** with full handler instrumentation
- **GDPR primitives**: data classification attributes, exporter/eraser contracts, consent tracking
- **Testing**: xUnit v3, Shouldly, Verify, Bogus, Testcontainers, NetArchTest, WireMock.Net
- **Agent-ready**: `CLAUDE.md` files at multiple levels, comprehensive ADRs, `dotnet new` item templates for slices and modules

## Quick start

```bash
# Clone and enter the directory
git clone <your-fork> my-api
cd my-api

# Restore and build
dotnet restore
dotnet build

# Run the full stack (Postgres, Redis, Mailpit, API) via Aspire
dotnet run --project src/AppHost

# Run the tests
dotnet test
```

## Working with AI agents

Modulith treats AI coding agents as first-class collaborators alongside human developers. The structural choices that make the codebase navigable for humans — vertical slices, explicit module boundaries, ADRs, exhaustive architectural tests — are the same choices that make it safe for agents to operate in autonomously. Neither audience is an afterthought.

### Layered guidance

Agent context is organized hierarchically so the right instructions are always close to the work:

- **`/CLAUDE.md`** — repo-wide operating manual: invariants, workflow, common commands, footguns
- **`/src/Modules/CLAUDE.md`** — module shape conventions and cross-module rules
- **`/src/Modules/<Module>/CLAUDE.md`** — per-module domain vocabulary and business rules
- **`/tests/CLAUDE.md`** — test patterns and what belongs in each layer
- **`/docs/adr/CLAUDE.md`** — ADR format guidance

### The `.claude/` harness

The `.claude/` directory ships an active harness that Claude Code picks up automatically.

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

**Sub-agents** handle focused tasks with tightly scoped tool access:

- `domain-modeler` — works exclusively in `Domain/` folders; writes unit tests alongside models
- `integration-tester` — writes integration tests with Testcontainers, Wolverine TrackActivity, and WireMock.Net
- `migration-writer` — adds EF Core migrations and summarizes the generated SQL
- `adr-drafter` — turns design conversations into ADR drafts for human review and commit

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
- [`docs/examples/`](docs/examples/) — worked patterns extracted from real modules (query slice, command+event, cross-module subscriber, scheduled job, security-sensitive slice)
- [`COMPLIANCE.md`](COMPLIANCE.md) — GDPR posture and compliance considerations
- [`CONFIG.md`](CONFIG.md) — configuration hierarchy and secrets management
- [`CLAUDE.md`](CLAUDE.md) — operating manual for AI agents working in this codebase

## License

MIT — see [LICENSE](LICENSE).

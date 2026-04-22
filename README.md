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

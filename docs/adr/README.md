# Architecture Decision Records

This directory contains ADRs documenting every significant design decision in Modulith.

## Index

| # | Title | Status |
|---|---|---|
| [0001](0001-modular-monolith.md) | Modular Monolith Architecture | Accepted |
| [0002](0002-vertical-slice-architecture.md) | Vertical Slice Architecture | Accepted |
| [0003](0003-wolverine-for-messaging.md) | Wolverine for Messaging, Outbox, and Background Jobs | Accepted |
| [0004](0004-result-pattern.md) | Result Pattern Over Exceptions | Accepted |
| [0005](0005-module-communication-patterns.md) | Module Communication via Contracts Projects | Accepted |
| [0006](0006-internal-vs-public-events.md) | Separate Internal Domain Events from Public Integration Events | Accepted |
| [0007](0007-no-aspnet-identity.md) | No ASP.NET Identity — Lightweight Custom User Aggregate | Accepted |
| [0008](0008-fluent-validation.md) | FluentValidation for Request Validation | Accepted |
| [0009](0009-rich-domain-model.md) | Rich Domain Model with Private Setters | Accepted |
| [0010](0010-serilog-and-otel.md) | Serilog Routed Through OpenTelemetry | Accepted |
| [0011](0011-auditing-strategy.md) | Two-Layer Auditing: Row-Level Fields and Dedicated Audit Module | Accepted |
| [0012](0012-gdpr-primitives.md) | GDPR Primitives Baked Into the Template | Accepted |
| [0013](0013-blob-storage-abstraction.md) | IBlobStore Abstraction with Local-Disk Reference Implementation | Accepted |
| [0014](0014-notifications-architecture.md) | Two-Layer Notifications: Transport and Orchestration | Accepted |
| [0015](0015-architectural-tests.md) | Architectural Tests for Boundary Enforcement | Accepted |
| [0016](0016-centralized-package-management.md) | Centralized Package and Build Configuration | Accepted |
| [0017](0017-hybrid-cache.md) | HybridCache for Data Caching | Accepted |
| [0018](0018-rate-limiting.md) | Built-in Rate Limiting with Tiered Policies | Accepted |
| [0019](0019-feature-flags.md) | Microsoft.FeatureManagement with Startup/Runtime Split | Accepted |
| [0020](0020-no-idempotency-infrastructure.md) | No Built-in Idempotency Infrastructure | Accepted |
| [0021](0021-config-and-secrets.md) | Strongly-Typed Options and Hierarchical Configuration | Accepted |
| [0022](0022-testing-strategy.md) | Four-Layer Testing Strategy | Accepted |
| [0023](0023-per-module-dbcontext.md) | DbContext and Schema Per Module | Accepted |
| [0024](0024-scalar-for-openapi.md) | Scalar for OpenAPI Documentation | Accepted |
| [0025](0025-problem-details-for-errors.md) | ProblemDetails for All Error Responses | Accepted |
| [0026](0026-module-seeders.md) | IModuleSeeder Contract for Deterministic Seed Data | Accepted |
| [0027](0027-agentic-development-support.md) | First-Class Support for Agentic Development | Accepted |

## Reading order

If you read five, read these in order:

1. [0001 — Modular Monolith Architecture](0001-modular-monolith.md)
2. [0002 — Vertical Slice Architecture](0002-vertical-slice-architecture.md)
3. [0003 — Wolverine for Messaging](0003-wolverine-for-messaging.md)
4. [0004 — Result Pattern](0004-result-pattern.md)
5. [0015 — Architectural Tests](0015-architectural-tests.md)

If you read ten, add:

6. [0005 — Module Communication](0005-module-communication-patterns.md)
7. [0009 — Rich Domain Model](0009-rich-domain-model.md)
8. [0023 — DbContext Per Module](0023-per-module-dbcontext.md)
9. [0007 — No ASP.NET Identity](0007-no-aspnet-identity.md)
10. [0027 — Agentic Development](0027-agentic-development-support.md)

Everything else is domain-specific and can be read as needed.

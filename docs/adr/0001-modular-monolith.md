# ADR-0001: Modular Monolith Architecture

## Status

Accepted

## Context

For a general-purpose API template, three architectural shapes were candidates:

1. **Traditional monolith** — one project, organized by layer (Controllers, Services, Repositories, Models). Simplest to start, hardest to maintain past a modest size, nearly impossible to split later.
2. **Microservices** — multiple services from day one. High operational cost, distributed-systems complexity paid up front, complexity compounds before product-market fit.
3. **Modular monolith** — a single deployable unit composed of internally modular components with enforced boundaries.

Most teams that start with microservices regret it. Most teams that start with a traditional monolith regret it differently. The modular monolith is a pragmatic middle ground: operational simplicity of one deployable, architectural hygiene closer to microservices, and a clean path to extraction when scale or team topology demands it.

## Decision

Modulith is a modular monolith. A single deployable host composed of modules. Each module is a vertical slice of business capability with its own domain model, persistence, endpoints, and public contract. Module boundaries are enforced at compile time (project references) and at test time (architectural tests).

Modules may communicate only through each other's public `.Contracts` projects — never by reaching into internal code. This discipline makes future extraction mechanical rather than architectural.

## Consequences

**Positive:**

- One deployable: simple CI/CD, simple ops, one log stream, one metrics destination.
- No distributed-systems tax: in-process calls, no network reliability concerns between modules, no service discovery, no versioning of wire protocols until/unless a module is extracted.
- Refactoring across modules is possible (via contracts changes) where it would be a multi-team negotiation in microservices.
- Clear path to extraction when needed — boundaries are real, schemas are already separate, contracts are already defined.
- Faster local dev: `dotnet run` and everything is up.

**Negative:**

- A single host: one bad deployment affects all modules.
- Discipline is required. A modular monolith without enforcement is a ball of mud in folders. Architectural tests (ADR-0015) and project reference rules (ADR-0005) are non-negotiable.
- Scaling is vertical until extraction. If one module has dramatically different resource needs, you either extract it or over-provision the whole host.
- Team independence is conceptual, not technical. Two teams working in two modules still share a build, share a test suite, share a deployment.

**Trade-off accepted:** operational simplicity now, at the cost of having to extract later if scale demands. The extraction cost is paid later, with more information, only if needed.

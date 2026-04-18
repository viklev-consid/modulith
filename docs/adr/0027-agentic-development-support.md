# ADR-0027: First-Class Support for Agentic Development

## Status

Accepted

## Context

AI coding agents are increasingly part of development workflows. Templates designed with only human developers in mind produce friction for agents: agents waste cycles re-deriving conventions, hallucinate file structures, misunderstand boundaries, and fail to ask clarifying questions.

A template that supports agents well does not compromise human ergonomics — the same structural clarity, documentation density, and enforcement that helps agents also helps humans. The question is whether to treat agent support as an explicit design goal or an incidental benefit.

Making it explicit changes several decisions:

- Documentation emphasis (multiple `CLAUDE.md` files, dense ADRs, glossary)
- Error message quality (architectural test failures written to be actionable)
- Self-scaffolding (`dotnet new` item templates)
- Capability boundaries (explicit lists of what agents shouldn't touch)
- Feedback loop speed (warnings-as-errors, `ValidateOnStart`, fast test tier)

## Decision

Treat first-class agent support as an explicit design goal. Implement the following mechanisms:

### 1. Scoped CLAUDE.md files

- `/CLAUDE.md` — repo-wide operating manual
- `/src/Modules/CLAUDE.md` — module conventions
- `/src/Modules/<Module>/CLAUDE.md` — per-module specifics (added with the module)
- `/tests/CLAUDE.md` — test patterns
- `/docs/adr/CLAUDE.md` — ADR format

Agents (and humans) read these based on working directory. The root one is terse (under 500 lines); specific ones add detail.

### 2. Comprehensive ADRs

Every significant decision has an ADR. ADRs explain *why* — the context humans and agents need before changing a rule.

### 3. Architectural tests with actionable failures

Failure messages name the rule, the type, and the fix:

> FAIL: Modulith.Modules.Orders.Persistence.OrdersDbContext depends on Modulith.Modules.Users.Persistence.UsersDbContext. Modules must not share DbContexts. Use Users' public Contracts (via IMessageBus) to request data. See ADR-0005 and ADR-0023.

### 4. Self-scaffolding via dotnet new

Item templates for slices and modules:

```bash
dotnet new slice --module Orders --name CancelOrder
dotnet new module --name Inventory
```

Produces correct file structure, namespaces, registrations. Agents (and humans) prefer scaffolding to manual creation — manual creation is where small inconsistencies enter.

### 5. Explicit capability boundaries

The root `CLAUDE.md` lists files and decisions agents should not touch autonomously:

- `Directory.Packages.props`
- `Directory.Build.props`
- `.editorconfig` / `.globalconfig`
- `Api/Program.cs` composition root
- Wolverine root configuration
- Aspire AppHost resource declarations
- Existing migrations
- ADRs (except new ones)
- CI pipeline

Changes to these require explicit instruction or an ADR.

### 6. Ask-vs-proceed guidance

Explicit lists in `CLAUDE.md` of:

- Cases where an agent should ask before acting
- Cases where autonomous action is appropriate

Reduces the cost of judgment calls under context limits.

### 7. Fast, deterministic feedback

- Warnings are errors (no noise to filter).
- `ValidateOnStart` — misconfigurations fail at boot with clear messages.
- Architectural tests in the fast tier (boundary mistakes fail within a minute).
- Shouldly's readable failure messages (ADR-0022).

### 8. Glossary and examples

`docs/glossary.md` defines terms as used in this codebase. `docs/examples/` will contain worked slices (post-implementation) covering common patterns — agents adapt examples more reliably than they derive from principles.

### 9. Documentation agents actually use

Reference-style over tutorial-style. Compiling, tested examples. Short sections with concrete headings. Cross-links.

## Consequences

**Positive:**

- Agents perform more of the mechanical work with less hand-holding.
- Humans benefit from the same clarity — CLAUDE.md files, ADRs, and glossary help anyone new to the codebase.
- Agent mistakes are structural rather than catastrophic — boundary violations fail fast, not in production.
- Mechanical scaffolding (via `dotnet new`) is consistent regardless of who (or what) creates a slice.

**Negative:**

- Upfront documentation effort is substantial. Accepted — pays back across every subsequent change.
- Documentation drift risk — docs that lie about the code are worse than no docs. Mitigated by keeping docs close to code, updating ADRs as decisions change, and by the fact that `dotnet new` templates and architectural tests constitute executable documentation.
- "Designed for agents" framing occasionally surprises reviewers who expect purely human-facing docs. The response: good docs are good docs.

## Related

All other ADRs in some sense — this ADR is the meta-decision that the codebase's documentation density and enforcement posture serve agents as much as humans. Particularly:

- ADR-0015 (Architectural Tests): enforcement quality matters for agents.
- ADR-0016 (Centralized Package Management): capability-bounded for agents.
- ADR-0022 (Testing Strategy): test failure quality is an agent concern.

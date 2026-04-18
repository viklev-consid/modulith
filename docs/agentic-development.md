# Agentic Development

Modulith is set up to be ergonomic for AI coding agents as well as humans. This document describes the mechanisms that enable that and the principles behind them. It is itself intended to be read by agents.

---

## Core principle

Agents perform best when the codebase *biases them toward the right action* under incomplete information. Humans can ask questions and form mental models over time; agents have a context window and have to infer or ask within it. Every mechanism below is an attempt to reduce inference and increase signal.

---

## Mechanisms

### Scoped `CLAUDE.md` files

Agent guidance is organized hierarchically:

- [`/CLAUDE.md`](../CLAUDE.md) — repo-wide operating manual. Invariants, workflow, common commands, footguns, capability boundaries.
- [`/src/Modules/CLAUDE.md`](../src/Modules/CLAUDE.md) — module-wide conventions. The shape of a module, how to add one, cross-module rules.
- `/src/Modules/<Module>/CLAUDE.md` — per-module specifics, added when the module exists. Domain vocabulary, business rules, known quirks.
- [`/tests/CLAUDE.md`](../tests/CLAUDE.md) — test patterns, fixture usage, what belongs in each layer.
- [`/docs/adr/CLAUDE.md`](adr/CLAUDE.md) — ADR format and how to write a new one.

Agents pick these up based on working directory. Read the root one first, then the most specific one for your current task.

### Architecture Decision Records

The `docs/adr/` directory contains one ADR per major decision. Each ADR explains:

- **Context** — why the decision was needed
- **Decision** — what was chosen
- **Consequences** — what follows from it, including trade-offs

ADRs are the primary way to answer "why is it like this?" They are append-only in normal operation — superseding an ADR means writing a new one that references the old.

Agents: when you see a rule in `CLAUDE.md`, the corresponding ADR explains why. Before arguing with a rule, read the ADR.

### Architectural tests as executable documentation

The rules in `CLAUDE.md` are not on the honor system — they are enforced by `tests/Modulith.Architecture.Tests`. When you violate a rule, the failing test tells you *what* rule and *how* to fix it.

The failure messages are written for agents. They are specific: type names, target namespaces, suggested actions. Read them literally.

### Self-scaffolding via `dotnet new` templates

Two `dotnet new` item templates ship with Modulith:

```bash
# Add a feature slice
dotnet new slice --module Orders --name CancelOrder

# Add a new module
dotnet new module --name Inventory
```

These generate the exact file set with correct namespaces, placeholder registrations, and stub tests. Agents should prefer these to manual scaffolding — manual scaffolding is how small inconsistencies creep in.

### Fast, specific feedback loops

- **Warnings are errors.** No warnings, so agents don't waste cycles deciding what to fix.
- **`ValidateOnStart()` on all Options.** Misconfiguration fails at boot with a clear message, not on first request with an obscure NRE.
- **Architectural tests run in the fast tier.** Boundary mistakes fail within a minute of running tests, not at PR review time.
- **Test failure messages are human-readable.** Shouldly's default diffs plus custom messages where needed.

### Glossary of terms

[`docs/glossary.md`](glossary.md) defines terms as used in this codebase. Agents use this to resolve ambiguity ("command" in DDD vs. "command" in MediatR terms vs. "command" in CLI terms) without having to infer from usage.

### Worked examples

Once modules are implemented, `docs/examples/` will contain non-trivial worked slices covering:

- Simple CRUD within a module
- Cross-module event publishing and subscription
- File upload via `IBlobStore`
- Long-running background job
- Scheduled job
- Notification triggered by a domain event

"Find the closest example and adapt" is a better agent strategy than "derive from principles." The examples make that strategy viable.

---

## Capability boundaries for agents

Agents should not modify the following autonomously — changes here require explicit instruction or an ADR:

- `Directory.Packages.props` — package version choices
- `Directory.Build.props` — shared MSBuild configuration
- `.editorconfig` / `.globalconfig` — style and analyzer configuration
- `Api/Program.cs` composition root — module registration order, host setup
- Wolverine root configuration — bus setup, transports, durability
- Aspire AppHost resource declarations
- Existing migrations
- `docs/adr/` — ADRs are appended (new files), not rewritten, except with explicit instruction
- CI pipeline configuration

If a task requires touching these, stop and ask.

---

## When to ask vs. proceed

Reproduced from `CLAUDE.md` for emphasis:

**Ask first if:**
- The change requires a new top-level dependency.
- The change alters module boundaries.
- The change affects the domain model AND changes public contracts.
- The change introduces a new cross-cutting concern.
- The request is ambiguous about module ownership.
- The change would violate an invariant.

**Proceed autonomously if:**
- Adding a new slice in an existing module.
- Adding a field to a DTO (with migration if persisted).
- Writing tests for existing functionality.
- Fixing a well-scoped bug.
- Refactoring within a single slice.

When in doubt, ask.

---

## How to read this codebase in order

If you're new to the codebase (human or agent):

1. [`README.md`](../README.md) — what this is
2. [`docs/architecture.md`](architecture.md) — the big picture
3. [`docs/glossary.md`](glossary.md) — the terms
4. [`CLAUDE.md`](../CLAUDE.md) — the operating manual
5. [`docs/adr/0001-modular-monolith.md`](adr/0001-modular-monolith.md) and on — the reasoning
6. A concrete module — pick Users or Orders. Read it end-to-end, including tests.
7. [`docs/how-to/add-a-slice.md`](how-to/add-a-slice.md) — when you're ready to add something

---

## What this setup does not do

To be explicit about limits:

- Does not replace code review. Agent output is reviewed the same as human output.
- Does not prevent subtle bugs. It prevents structural mistakes, not logical ones.
- Does not mean agents can do architecture. Design decisions still belong to humans.
- Does not mean agents don't need to read code. Context files are supplements, not substitutes.

The goal is to reduce the structural cost of agent involvement, not to eliminate human judgment.

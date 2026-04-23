# Agentic Development

This document explains how Modulith is structured for agentic development — what mechanisms are in place, why they are designed the way they are, and what a developer adopting this template should understand about the agent experience alongside their own.

---

## Core principle

Agents perform best when the codebase *biases them toward the right action* under incomplete information. Humans can ask questions and form mental models over time; agents have a context window and have to infer or ask within it. Every mechanism below is an attempt to reduce inference and increase signal.

---

## Mechanisms

The mechanisms below fall into two categories: *passive* guidance that shapes agent behaviour through context and structure, and *active* enforcement that prevents mistakes at the moment they would be made. The passive mechanisms are described in this section; the active harness is described in [Active enforcement: the `.claude/` harness](#active-enforcement-the-claude-harness) below.

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
dotnet new modulith-slice --module Orders --name CancelOrder

# Add a new module
dotnet new modulith-module --name Inventory
```

These generate the exact file set with correct namespaces and placeholder registrations. Agents should prefer these to manual scaffolding — manual scaffolding is how small inconsistencies creep in.

### Fast, specific feedback loops

- **Warnings are errors.** No warnings, so agents don't waste cycles deciding what to fix.
- **`ValidateOnStart()` on all Options.** Misconfiguration fails at boot with a clear message, not on first request with an obscure NRE.
- **Architectural tests run in the fast tier.** Boundary mistakes fail within a minute of running tests, not at PR review time.
- **Test failure messages are human-readable.** Shouldly's default diffs plus custom messages where needed.

### Glossary of terms

[`docs/glossary.md`](glossary.md) defines terms as used in this codebase. Agents use this to resolve ambiguity ("command" in DDD vs. "command" in MediatR terms vs. "command" in CLI terms) without having to infer from usage.

### Worked examples

`docs/examples/` contains worked slices extracted from real modules. Planned coverage:

- Simple CRUD within a module
- Cross-module event publishing and subscription
- File upload via `IBlobStore`
- Long-running background job
- Scheduled job
- Notification triggered by a domain event

"Find the closest example and adapt" is a better agent strategy than "derive from principles." The examples make that strategy viable.

---

## Active enforcement: the `.claude/` harness

The mechanisms above are passive — they provide context and structure that bias agents toward correct behaviour. The `.claude/` harness is active: it enforces rules at the point they would be violated, surfaces failures immediately within the conversation, and provides scoped workflows that prevent whole classes of mistake before they reach code review.

### Hooks

Hooks are shell scripts wired to Claude Code lifecycle events. They run automatically — no invocation required.

**`session-context.sh` — SessionStart**

Injects a compact orientation into every session: the opening section of `CLAUDE.md`, the current branch and recent commits, the list of present modules, and a summary of the guardrails enforced by the other hooks. Every session starts with current project state rather than stale assumptions.

**`guard-paths.sh` — PreToolUse**

Runs before every file write. Blocks:

- Writes to `docs/adr/` — ADRs are human-committed
- Writes to `appsettings.Production.json`
- Domain files with forbidden `using` directives (EF Core, ASP.NET Core, Wolverine, Serilog, etc.)
- Files in one module that reference another module's non-Contracts namespaces

The domain purity and cross-module checks enforce invariants 2 and 1 from `CLAUDE.md` *before* the file is written, not after a test run.

**`post-edit-dotnet.sh` — PostToolUse**

After each `.cs` edit in `src/`, runs a scoped format check, build, and (for Domain or module-root files) architecture tests on the touched project. Failures are injected back into the conversation as additional context. The agent sees them immediately and can correct course before moving on to the next file, rather than discovering a broken build ten edits later.

**`slice-completeness.sh` — PostToolUse**

After each `.cs` edit inside a `Features/<Slice>/` folder, checks that all six slice files exist (Request, Response, Command, Handler, Validator, Endpoint). Reports any missing files so incomplete scaffolding is noticed immediately rather than silently left behind.

**`stop-format.sh` — Stop**

Before the session ends, runs `dotnet format` on all projects with changed files and reports what was auto-fixed. The working tree is always left tidy without requiring a separate formatting step.

**`stop-gate.sh` — Stop**

Blocks the session from ending if architecture tests are failing. This is the backstop: even if earlier feedback was dismissed or bypassed, a session with broken structural rules cannot close cleanly. The agent must resolve the failures before handing back.

### Slash commands

Commands are prompt files under `.claude/commands/` — scoped workflows with explicitly listed tool permissions. Each command only has access to the tools it actually needs, which prevents accidental side effects.

- **`/check`** — runs format verification, build, architecture tests, and unit tests scoped to projects changed in the current session. Use before wrapping up a task.
- **`/new-slice <Module> <Feature>`** — scaffolds a vertical slice via `dotnet new modulith-slice`, verifies all six files were generated, and reports what the developer needs to fill in. Preferred over manual scaffolding.
- **`/new-module <Name>`** — scaffolds a new module with an explicit confirmation step before running anything, then lists the follow-ups that belong to the developer (host registration, per-module `CLAUDE.md`, ADR if warranted).
- **`/new-adr <title>`** — finds the next ADR number, drafts a complete record in Nygard format in chat, and reminds the developer to commit it. Never writes the file — that decision belongs to a human.

### Sub-agents

Sub-agents are specialized agent definitions under `.claude/agents/`. Each has a narrow responsibility and a tightly scoped tool list. The main conversation delegates to them for focused tasks rather than expanding scope inline.

| Sub-agent | Responsibility | Out of scope |
|---|---|---|
| `domain-modeler` | Domain entities, value objects, aggregates, domain events + unit tests | Persistence, handlers, endpoints, DI |
| `integration-tester` | Integration tests using Testcontainers, Wolverine TrackActivity, WireMock.Net | Unit tests, arch tests, production code changes |
| `migration-writer` | EF Core migrations for a single module; summarizes generated SQL | Applying migrations, domain or config changes |
| `adr-drafter` | ADR drafts in chat for human review and commit | Writing any file, editing existing ADRs |

Each sub-agent is defined to stop and escalate when the task drifts outside its scope — production code changes don't creep into test-writing sessions, and domain modeling doesn't drift into infrastructure configuration.

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

1. [`README.md`](../README.md) — what this is and how agents fit in
2. [`docs/architecture.md`](architecture.md) — the big picture
3. [`docs/glossary.md`](glossary.md) — the terms
4. [`CLAUDE.md`](../CLAUDE.md) — the operating manual
5. [`docs/adr/0001-modular-monolith.md`](adr/0001-modular-monolith.md) and on — the reasoning
6. A concrete module — pick Users or Catalog. Read it end-to-end, including tests.
7. [`docs/how-to/add-a-slice.md`](how-to/add-a-slice.md) — when you're ready to add something
8. [`.claude/`](../.claude/) — review the hooks, commands, and sub-agent definitions to understand what runs automatically and when

---

## What this setup does not do

To be explicit about limits:

- Does not replace code review. Agent output is reviewed the same as human output.
- Does not prevent subtle bugs. It prevents structural mistakes, not logical ones.
- Does not mean agents can do architecture. Design decisions still belong to humans.
- Does not mean agents don't need to read code. Context files are supplements, not substitutes.

The goal is to reduce the structural cost of agent involvement, not to eliminate human judgment.

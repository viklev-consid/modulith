# CLAUDE.md — Architecture Decision Records

This directory contains ADRs — Architecture Decision Records — one per significant design decision in Modulith.

## Format

We use the Michael Nygard format:

```
# ADR-NNNN: Title

## Status
Accepted | Proposed | Superseded by ADR-XXXX | Deprecated

## Context
What is the issue we're facing? What forces are at play?

## Decision
What did we decide to do? Stated in the active voice.

## Consequences
What follows from this decision — both the good and the bad?
```

Keep ADRs short. One page is ideal. If an ADR needs more than two pages, the decision probably needs splitting.

## Rules

- **Appending, not editing.** Once an ADR is accepted, don't rewrite it. Write a new ADR that supersedes it.
- **Numbered sequentially.** `0001`, `0002`, etc. No gaps, no reordering.
- **Written when the decision is made.** Not retroactively. Decisions without ADRs accumulate as tacit knowledge and rot.
- **Link liberally.** If ADR-0005 builds on ADR-0002, say so and link.

## When to write an ADR

Write one for:
- Any decision that affects multiple modules.
- Any decision that will be surprising to someone reading the code six months later.
- Any decision that closes off future options.
- Any decision you'd want to justify in code review more than once.

Don't write one for:
- Local choices within a single slice.
- Obvious consequences of an existing ADR.
- Decisions that haven't been made yet — write a proposal instead.

## Naming

`NNNN-kebab-case-title.md`. Title is the decision, not the question. "Result Pattern Over Exceptions" not "How Should We Handle Errors".

## Current ADRs

See the directory listing. A summary of all ADRs lives in [`README.md`](README.md) in this directory.

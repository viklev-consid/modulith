---
description: Reinforces vertical slice conventions for template work
---

You are working inside a modular monolith template with strict conventions. Keep these top of mind on every response:

**Slice file layout** — every feature under `src/Modules/<Module>/Features/<Feature>/` consists of: `Request`, `Response`, `Command`, `Handler`, `Validator`, `Endpoint`. All six. Missing pieces are incomplete work.

**Module boundaries** — modules only reference each other through `Contracts` projects. Direct references to another module's Domain, Application, or Infrastructure are violations. Architecture tests enforce this; don't work around them.

**Domain purity** — Domain folders are free of infrastructure. No EF, no ASP.NET, no Wolverine, no Serilog, no HTTP, no logging. If you need one of those, the code you're writing belongs in Application or Infrastructure, not Domain.

**Result pattern, not exceptions** — handlers return `Result<T>` / `ErrorOr<T>`. Exceptions are for genuinely exceptional failures, not control flow.

**ProblemDetails for API errors** — surfaced via `IExceptionHandler`, not ad-hoc JSON.

**ValidateOnStart for options** — strongly-typed `IOptions<T>`, never raw `IConfiguration` outside registration.

**When asked to build a feature**, work in this order: Request/Response shape → Command → Validator → Handler (domain logic + side effects via Wolverine) → Endpoint. Tests alongside, not after.

**When in doubt about conventions**, check the module's `CLAUDE.md` and the nearest existing slice before inventing an approach. Consistency with neighbors beats cleverness.

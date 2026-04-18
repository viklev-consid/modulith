# ADR-0002: Vertical Slice Architecture

## Status

Accepted

## Context

Within a module, code needs to be organized. The two traditional approaches are:

1. **Horizontal layering** (Clean/Onion/Hexagonal): folders named `Controllers/`, `Services/`, `Repositories/`, `Domain/`. A single feature touches files across every folder.
2. **Vertical slices**: folders named per feature (`PlaceOrder/`, `CancelOrder/`, `GetOrderById/`). All files for one feature live together.

Horizontal layering optimizes for "I want to see all controllers" or "I want to see all repositories." Vertical slicing optimizes for "I want to change this feature." In practice, the second question is asked thousands of times more often than the first.

Horizontal layering also produces ceremony that isn't free: every feature accretes a DTO, a service interface, an implementation, a repository interface, an implementation, validation attributes, AutoMapper profiles. Most of it exists to satisfy the layer, not the feature.

## Decision

Each module organizes its features as vertical slices under a `Features/` folder. A slice contains exactly these files, co-located:

- `{Feature}.Request.cs` — the HTTP request DTO
- `{Feature}.Response.cs` — the HTTP response DTO
- `{Feature}.Command.cs` or `{Feature}.Query.cs` — the internal message
- `{Feature}.Handler.cs` — the Wolverine handler
- `{Feature}.Validator.cs` — the FluentValidation validator
- `{Feature}.Endpoint.cs` — the minimal API endpoint registration

Domain concerns (aggregates, value objects, domain events) live in the module's `Domain/` folder, not in slices. Persistence (DbContext, EF configurations, migrations) lives in `Persistence/`. Integration handlers for events from other modules live in `Integration/`.

## Consequences

**Positive:**

- Changes are localized. Adding a field to a request usually touches two files in one folder.
- Delete-safe. Deleting a feature means deleting a folder. No orphaned services, no dead DTOs.
- Onboarding friendly. A new developer can read one slice and understand a feature completely.
- Low ceremony. Six files, no abstractions that exist only for symmetry.
- Natural pairing with mediation (Wolverine). Request → Command → Handler → Result is already a good vertical structure.

**Negative:**

- Risk of drift toward anemic models. When the handler is right there, it's easy to write procedural code against entities. Mitigated by ADR-0009 (rich domain model) and architectural tests that forbid public setters.
- Some duplication across slices. Two slices that validate "email is unique" both know the rule. This is usually fine — explicit duplication is cheaper than premature abstraction.
- Less obvious where shared slice-support code goes. The answer: module-internal helpers can live at the module root, but genuinely shared code goes to `Shared.Kernel` or `Shared.Infrastructure`.

**Trade-off accepted:** optimize for the change-a-feature case, at the cost of losing some architectural symmetry.

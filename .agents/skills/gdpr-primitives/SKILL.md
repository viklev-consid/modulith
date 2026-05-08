---
name: gdpr-primitives
description: Technical workflow for adding personal-data handling in Modulith. Covers data classification, export and erasure hooks, consent integration, retention, and testing.
---

# GDPR Primitives

Use this skill when a feature adds, changes, exports, erases, retains, or otherwise processes user personal data.

Typical triggers:

- adding a new entity or field that stores personal data
- implementing export or erasure for a module
- introducing user-scoped cached or retained data
- wiring consent-aware behavior

Do not use this skill when:

- the task is legal policy drafting
- the change is generic authorization or authentication with no personal-data handling impact
- the task is only a normal slice with no GDPR-relevant data

## Read first

Before changing personal-data handling, read:

1. `COMPLIANCE.md`
2. `docs/how-to/gdpr-features.md`
3. `docs/adr/0012-gdpr-primitives.md`
4. one existing module `Gdpr/` implementation
5. the shared interfaces in `src/Shared/Modulith.Shared.Kernel/Interfaces/`
6. `src/Modules/Users/Modulith.Modules.Users.Contracts/ConsentKeys.cs` when consent is involved

## This is technical guidance, not legal advice

This skill helps keep the codebase consistent with the template's GDPR mechanisms.

It does not decide:

- lawful basis
- retention policy legality
- privacy policy content
- regulatory interpretation

When those are unclear, escalate to humans.

## Classify personal data

Mark personal data where it is actually stored or represented.

Use:

- `[PersonalData]`
- `[SensitivePersonalData]`

Apply attributes consistently on fields or properties that hold personal data.

Why this matters:

- logging masks classified values
- exporters include the right fields
- data maps can be generated from the attributes

If data is personal and unmarked, the template cannot protect or export it correctly.

## Shared contracts to use

Do not invent new per-module GDPR interfaces when the shared ones already exist.

The live shared contracts are:

- `IPersonalDataExporter`
- `IPersonalDataEraser`
- `IConsentRegistry`
- `IRetainable`
- `PersonalDataExport`
- `ErasureResult`
- `UserRef`
- `ErasureStrategy`

For consent-aware features, also use the public consent keys owned by the Users contracts project.

Follow the current shared interface signatures from `Shared.Kernel`, not outdated examples.

## Exporter rules

If a module stores personal data, it should implement `IPersonalDataExporter`.

Implementation guidelines:

- query only that module's owned data
- shape the result into the shared `PersonalDataExport` record
- use stable, readable keys in the returned data dictionary
- return an empty module payload when the user has no data in that module

Keep exports module-scoped. The Users module orchestrates aggregation.

## Eraser rules

If a module stores user-linked personal data, it should implement `IPersonalDataEraser` unless the module is explicitly marked as holding no personal data.

Choose the strategy per module:

- hard delete
- anonymize
- tombstone or custom retention-preserving scrub

Good erasers:

- modify only the module's own data
- return a precise `ErasureResult`
- handle the requested `ErasureStrategy`
- keep retained records legally useful but non-identifying when anonymization is required

## No-personal-data opt out

If a module truly holds no personal data, prefer the explicit `[NoPersonalData]` assembly-level marker rather than silently omitting GDPR hooks.

Do not use the opt out as a shortcut when the module actually holds user-linked data.

## Registration rules

Register GDPR services in the module's service registration.

Typical registrations include:

- `services.AddScoped<IPersonalDataExporter, ...>()`
- `services.AddScoped<IPersonalDataEraser, ...>()`

Do not create a valid exporter or eraser and then forget to register it.

## Consent rules

Consent is a technical and domain concept distinct from preferences.

Rules:

- the Users module owns consent state; other modules read and react to it, they do not own parallel consent stores by default
- use the shared `IConsentRegistry` rather than inventing a new consent lookup
- the live `IConsentRegistry` contract is `HasConsentedAsync(Guid userId, string consentKey, ...)`, plus `GrantAsync(...)` and `RevokeAsync(...)`
- use stable consent-key constants from `Modulith.Modules.Users.Contracts.ConsentKeys`, not ad-hoc strings scattered through modules
- keep legal consent distinct from user preference toggles
- treat consent as an append-only history of grant and revoke records, not as a mutable flag in each consuming module
- when a feature depends on consent, consult the registry at the point where the processing decision is made
- if a feature grants or revokes consent, route that behavior through the Users-owned consent model rather than silently toggling a local preference row

Do not conflate marketing consent with notification preference settings.

Do not confuse consent with authorization. Consent answers whether the user agreed to a processing purpose; authorization answers whether the caller may perform an action.

## Retention rules

When data should expire after a defined retention window, model that explicitly.

Use `IRetainable` for entities that have a retention lifecycle, then implement a scheduled sweep strategy at the module level.

Do not hard-delete retained records if the module's strategy is anonymization or archive-preserving retention.

## Cache and log rules

If the module caches user-scoped data:

- invalidate or remove that cache when erasure occurs

If the module logs objects containing personal data:

- rely on the classification attributes so the shared logging policy can mask values

Do not assume logs are safe if the data was never marked.

## Boundary rules

GDPR handling remains module-scoped.

- each module exports and erases its own data
- the Users module orchestrates the user-facing flow
- modules do not reach across boundaries to erase another module's records directly

## Testing rules

At minimum, test:

- the module exporter returns the expected shaped data
- the eraser removes or anonymizes the correct records
- retained data follows the module's strategy

For flows exposed to users, prefer integration coverage that exercises export or erasure end to end.

If the module depends on consent, add tests that prove processing is blocked or allowed appropriately.

For consent-aware features, also test that the code uses the right consent key and that revocation changes behavior in the expected direction.

## Common mistakes

Avoid these:

- adding personal data fields without classification attributes
- implementing an exporter but not an eraser for user-linked data
- hard-deleting records that should be retained and anonymized
- leaving user-scoped cache entries intact after erasure
- inventing a separate module-local consent abstraction
- hardcoding consent-key strings instead of using shared contract constants
- treating consent as a local mutable boolean on a consuming module's entity
- treating preference flags as legal consent
- following an outdated example instead of the current shared interface shape

## Ask-first cases

Stop and ask before proceeding if:

- the lawful basis or retention policy is unclear
- the change crosses module boundaries in a new way
- the data classification itself is ambiguous

## Definition of done

A GDPR-relevant change is complete when:

- personal data is classified with the appropriate attributes
- modules with personal data implement or explicitly opt out of export and erasure hooks
- exporter and eraser implementations are registered
- consent and retention are handled through the shared abstractions where applicable
- caches and logs do not re-expose erased or masked personal data
- relevant tests cover export, erasure, and consent-sensitive behavior

## Reference material

Use these as the source of truth:

- `COMPLIANCE.md`
- `docs/how-to/gdpr-features.md`
- `docs/adr/0012-gdpr-primitives.md`
- `src/Shared/Modulith.Shared.Kernel/Interfaces/IPersonalDataExporter.cs`
- `src/Shared/Modulith.Shared.Kernel/Interfaces/IPersonalDataEraser.cs`
- `src/Shared/Modulith.Shared.Kernel/Interfaces/IConsentRegistry.cs`
- `src/Shared/Modulith.Shared.Kernel/Gdpr/PersonalDataExport.cs`
- `src/Shared/Modulith.Shared.Kernel/Gdpr/ErasureResult.cs`
- `src/Modules/Users/Modulith.Modules.Users.Contracts/ConsentKeys.cs`
- `src/Modules/Users/Modulith.Modules.Users/ConsentManagement/UsersConsentRegistry.cs`

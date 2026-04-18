# ADR-0012: GDPR Primitives Baked Into the Template

## Status

Accepted

## Context

GDPR (and similar regulations) impose requirements that are painful to retrofit:

- **Right to access** — users may request an export of their personal data.
- **Right to erasure** — users may request deletion of their personal data.
- **Consent tracking** — lawful processing often requires recorded consent.
- **Data minimization** — don't store or log more than necessary.
- **Retention limits** — data must be removed after its purpose expires.

A template that ignores GDPR forces every project to build these mechanisms late, usually under compliance pressure, usually badly. A template that over-prescribes lock-step implementations is equally bad because retention policies and erasure strategies are context-specific.

The right shape is: bake in the *primitives* (classification, export/erase contracts, consent tables, retention hooks), and document the strategies.

## Decision

The template ships with the following primitives:

### 1. Data classification attributes

In `Shared.Kernel`:

```csharp
[AttributeUsage(AttributeTargets.Property)]
public sealed class PersonalDataAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitivePersonalDataAttribute : Attribute { }
```

Properties on DTOs, entities, and events are marked with these. The attributes drive:

- Serilog destructuring (masking — ADR-0010)
- Export generation (inclusion in personal data export)
- Documentation (a `data-map.md` generator can enumerate classified properties)

### 2. Export and erase contracts

Each module that holds personal data implements one or both:

```csharp
public interface IPersonalDataExporter
{
    Task<PersonalDataExport> ExportAsync(UserRef user, CancellationToken ct);
}

public interface IPersonalDataEraser
{
    Task<ErasureResult> EraseAsync(UserRef user, ErasureStrategy strategy, CancellationToken ct);
}
```

The Users module aggregates these: a `GET /users/me/personal-data` endpoint calls all registered exporters and packages the result. A `DELETE /users/me` endpoint publishes `UserErasureRequested`, and each module's eraser handles it per its own strategy (anonymize vs. hard-delete vs. soft-delete with retention).

Modules decide their own erasure strategy; the template does not prescribe. The Audit module, for example, typically anonymizes rather than erases (legitimate interest basis).

### 3. Consent tracking

The Users module owns a `Consents` table:

| Column | Meaning |
|---|---|
| UserId | The user |
| Purpose | Canonical string identifying the consent purpose (`marketing-emails`, `analytics`) |
| GrantedAt | When consent was granted (null if never granted) |
| RevokedAt | When consent was revoked (null if still active) |
| PolicyVersion | The version of the privacy policy in effect at grant time |

A `ConsentRegistry` service exposes `IsGranted(UserId, Purpose)` to other modules. Consent is data, not UX — the Users module stores it; other modules read it.

### 4. Retention hooks

`Shared.Kernel` defines `IRetainable`:

```csharp
public interface IRetainable
{
    TimeSpan RetentionPeriod { get; }
    DateTimeOffset RetentionStartsAt { get; }
}
```

A scheduled Wolverine job sweeps `IRetainable` entities past their retention period. The per-module implementation of the sweep is the module's responsibility (some anonymize, some delete, some archive).

### 5. Documentation

`COMPLIANCE.md` at the repo root documents:

- Which modules hold personal data and what kind
- Cross-border transfer considerations (none by default — deployment choice)
- Backup handling (backup retention is the team's responsibility; pointers to provider docs)
- Data maps (can be generated from classification attributes)

## Deliberately NOT included

- **Encryption of PII at rest beyond what the DB provides.** Documented as an extension point. Crypto-shredding (encrypt + discard-key) is referenced but not implemented.
- **A consent-management UI.** Teams build their own or use a third-party.
- **A privacy dashboard.** Same.
- **Automatic erasure.** Always a policy decision; template provides the wiring, not the decision.

## Consequences

**Positive:**

- GDPR is addressable in a day, not a quarter. Classification and contracts are there; implementations are small.
- The Serilog masking is automatic — no log leaks once properties are classified.
- Audit considerations are built in (ADR-0011 integration).
- Consent tracking is typed and queryable, not ad-hoc.

**Negative:**

- Classification requires discipline. Unmarked PII will leak through logs and exports. Mitigated by code review and by the `PersonalDataAnalyzer` Roslyn analyzer (planned, not required).
- The eraser contract is per-module and modules can forget to implement it. Arch test: every module with entities referencing `UserId` must either implement `IPersonalDataEraser` or explicitly opt out via `[NoPersonalData]` on the module.
- Real compliance requires legal review, not just code. The template reduces technical risk, not legal risk.

## Related

- ADR-0010 (Serilog): destructuring policy masks classified properties.
- ADR-0011 (Auditing): audit entries typically anonymize rather than erase.
- ADR-0017 (HybridCache): cached PII must be flushed on erasure.

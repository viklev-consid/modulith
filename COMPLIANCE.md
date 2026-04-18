# COMPLIANCE.md

Modulith is designed with GDPR compliance as a first-class concern. This document describes the compliance posture of the template and the responsibilities that remain with teams using it.

**This document is technical guidance, not legal advice.** Regulatory interpretation, policy drafting, and jurisdictional questions require legal counsel.

---

## What the template provides

### Data classification

`[PersonalData]` and `[SensitivePersonalData]` attributes (in `Shared.Kernel`) mark properties holding personal data. The attributes drive:

- **Log masking** — classified properties are replaced with `***` in log output via Serilog destructuring policy (ADR-0010).
- **Export inclusion** — personal data exporters include classified fields.
- **Data mapping** — a documentation generator can enumerate classified properties across modules.

Modules must classify their data. Unmarked personal data leaks through logs and is missed in exports.

### Right to access (data export)

`IPersonalDataExporter` is a per-module contract. Each module that holds personal data implements it. The Users module exposes an aggregating endpoint:

```
GET /v1/users/me/personal-data
```

This returns a package combining output from all registered exporters, in JSON.

### Right to erasure

`IPersonalDataEraser` is a per-module contract. Strategy is module-specific:

- **Hard delete** (user account, preferences, notification log): the record is removed.
- **Anonymize** (orders, audit): personal fields are scrubbed; the record persists for retention.
- **Custom** (archiving, tombstoning): per-module implementation.

The Users module exposes:

```
DELETE /v1/users/me
```

This publishes `UserErasureRequestedV1`; each module's eraser subscribes and handles its scope. An eraser reference ID is returned to the user; a confirmation notification is sent on completion.

**Arch test enforcement:** modules with entities referencing a `UserId` must either implement `IPersonalDataEraser` or be marked `[assembly: NoPersonalData]`.

### Consent tracking

The Users module owns a `Consents` table:

| Field | Meaning |
|---|---|
| UserId | The user |
| Purpose | Canonical string identifying the consent purpose |
| GrantedAt | When consent was granted |
| RevokedAt | When consent was revoked (null if active) |
| PolicyVersion | The version of the privacy policy in effect at grant time |

Other modules check consent via `IConsentRegistry`. Revoking consent flips the matching notification preference automatically.

### Retention

Entities implementing `IRetainable` are swept by a scheduled Wolverine job past their retention period. Each module defines its sweep logic (anonymize / delete / archive).

Retention periods ship as illustrations only. Real retention values come from legal review.

### Audit

The Audit module (ADR-0011) records change history via domain events. Audit entries are treated as legitimate-interest data (required for compliance): on user erasure, the actor field is anonymized rather than the entry deleted.

### Log masking

Serilog's destructuring policy masks any property carrying `[PersonalData]` or `[SensitivePersonalData]`, plus properties matching common name patterns (`password`, `token`, `secret`, `apikey`). Applies to both structured properties and exceptions.

**Test coverage:** integration tests include assertions that log output for representative events does not leak personal data.

---

## What the template does NOT provide

Explicitly out of scope:

### Encryption at rest beyond what the database provides

Postgres TDE / Azure-managed encryption / RDS encryption covers most requirements. If field-level encryption is required (medical, financial, unusual regulatory), that is a per-field extension the team implements.

### Crypto-shredding

The pattern (encrypt PII with a per-user key; discard the key on erasure) is documented as an extension point. Not implemented.

### Privacy policy / consent UI

The Users module stores consent as data. The UI for granting/revoking consent is application-specific and not shipped.

### Automatic anonymization / tokenization

Each module decides its erasure strategy. No cross-module anonymization engine.

### DPIA, ROPA, or other formal compliance documentation

These are process artifacts, not code. Template does not attempt to generate them.

### Legal text

Privacy policies, data processing agreements, cookie banners — all out of scope. Use a specialized tool or counsel.

---

## Responsibilities that remain with the team

### Legal

- Drafting the privacy policy
- Determining lawful bases for each processing purpose
- Setting retention periods appropriate to the jurisdiction
- Reviewing cross-border data transfers (deployment-specific)
- Drafting data processing agreements with sub-processors
- Responding to regulatory inquiries
- Handling data subject requests that require human review (complex erasure cases, dispute resolution)

### Operations

- Ensuring backups are covered by retention policies (deleted data may persist in backups — policy must cover this)
- Controlling access to production data
- Audit logging infrastructure access
- Configuring jurisdiction-appropriate data residency in cloud providers
- Notifying regulators of breaches within the required window

### Engineering

- Classifying personal data as it's added (attributes)
- Implementing exporters and erasers when modules hold personal data
- Testing the erasure flow end-to-end
- Reviewing logs for inadvertent PII leakage
- Flagging new processing purposes for legal review before deployment

---

## Deployment concerns

### Cross-border transfers

If your deployment spans jurisdictions with different data protection regimes, data residency must be configured at the infrastructure layer:

- Postgres instance in-region
- Blob storage in-region
- Backup destinations in-region
- Logging/observability destinations in-region (or appropriately DPA-covered)

The template does not enforce this. Cloud provider configuration does.

### Backups

Regulatory retention typically does not extend to backups — "we deleted the user" must account for the user still existing in backup snapshots for the backup's retention period. Options:

- Short backup retention (covered by documented policy).
- Periodic backup restoration + re-erasure (complex, rare).
- Accept eventual erasure in backups via natural rotation.

Document the chosen approach. The template does not implement any of these.

### Sub-processors

Email providers, SMS providers, blob storage providers, analytics providers — each is a sub-processor that must be covered by DPAs. Teams choose providers and execute DPAs; template is provider-agnostic.

---

## Data map

A data map (which module holds which personal data) should be generated and kept current. The template provides the mechanism: attribute-based classification can be scanned to produce the map.

Example entries (once modules are implemented):

| Module | Category | Fields | Lawful basis | Retention |
|---|---|---|---|---|
| Users | Identity | Email, DisplayName, PasswordHash | Contract | Until erasure |
| Users | Contact | PhoneNumber | Contract | Until erasure |
| Users | Consent | Purpose, GrantedAt, RevokedAt | Legal obligation | 7 years |
| Orders | Transaction | CustomerId, ShippingAddress | Contract | 7 years (tax) |
| Audit | Activity | Actor, Action, EntityId | Legitimate interest | 1 year |
| Notifications | Preferences | UserId, Channel, Enabled | Consent (marketing) / Contract (transactional) | Until erasure |

Fill this in per your deployment.

---

## Verification checklist

When preparing for compliance review:

- [ ] Every personal data field is marked with `[PersonalData]` or `[SensitivePersonalData]`.
- [ ] Every module holding personal data implements `IPersonalDataExporter`.
- [ ] Every module with entities referencing `UserId` implements `IPersonalDataEraser` or is marked `[NoPersonalData]`.
- [ ] The erasure flow is exercised end-to-end in integration tests.
- [ ] Logs for representative operations do not leak PII (verified via tests).
- [ ] Consent is checked before each marketing notification.
- [ ] Retention periods are defined for `IRetainable` entities and verified by legal.
- [ ] Data map is current.
- [ ] Cross-border transfer assessment is complete (infrastructure-level).
- [ ] Backup retention policy is documented and aligned with data retention.
- [ ] Privacy policy references match the implemented flows.

---

## Related

- [`docs/adr/0012-gdpr-primitives.md`](docs/adr/0012-gdpr-primitives.md)
- [`docs/adr/0010-serilog-and-otel.md`](docs/adr/0010-serilog-and-otel.md)
- [`docs/adr/0011-auditing-strategy.md`](docs/adr/0011-auditing-strategy.md)
- [`docs/how-to/gdpr-features.md`](docs/how-to/gdpr-features.md)

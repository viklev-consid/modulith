# ADR-0034: Backend-Owned Versioned Legal Documents

## Status

Accepted

## Context

The Users module already records Terms of Service acceptance during onboarding, but a boolean acceptance and an externally supplied version string do not prove what text the user saw. The frontend also needs enough information to render Terms of Service and Privacy Policy content during onboarding and when an already-onboarded user must accept a newer version.

Legal document content has competing pressures:

- The backend owns the audit trail and must be able to verify acceptances.
- The frontend owns presentation and should render the document copy, but should not hardcode legal text.
- Terms and policies change rarely, but when they do, the system must distinguish accepted versions from current missing versions.
- Real deployments need legal content available without manual SQL or migration edits for every document update.
- Historical availability is useful, but exposing every superseded version is a separate product and legal-retention decision.

## Decision

The Users module owns legal document content as versioned Markdown stored in its database.

Current Terms of Service and Privacy Policy Markdown ships as embedded resources under the Users module's `LegalDocuments/` folder. `LegalDocumentsSeeder` reads the configured `Modules:Users:TermsOfServiceVersion` and `Modules:Users:PrivacyPolicyVersion`, inserts missing document rows, and runs outside the development-only seeder branch because legal documents are application data, not sample data.

Clients fetch legal requirements from the backend, render the returned Markdown, and echo back document ID, version, and content hash when accepting. The backend verifies the hash before writing an immutable acceptance record.

Continued-use requirements are checked globally for authenticated `/v1/*` requests. A blocking missing document returns HTTP 428 `ProblemDetails` with the missing documents embedded so the client can render the prompt. GDPR export, account deletion, logout, onboarding, legal compliance, legal acceptance, and current legal-document GET endpoints remain available while blocked.

The current public document-content endpoint only exposes current non-superseded document versions. Superseded versions are retained internally but are not a public historical archive.

## Consequences

**Positive:**

- The acceptance audit trail is tied to the exact document hash the user saw.
- The frontend can render legal copy without owning or duplicating legal text.
- Publishing a new current version is a backend/configuration operation, not a frontend release.
- Existing users can be gated consistently when a blocking continued-use document becomes active.
- Legal content remains module-owned and follows the same vertical-slice/error/ProblemDetails conventions as the rest of Users.

**Negative:**

- Legal document publishing now has operational semantics: the Markdown file, configured version, and database row must agree.
- The startup seeder has to be race-tolerant in multi-replica deployments.
- Current compliance checks sit in the authenticated request path, so they need caching and explicit invalidation.
- Historical display is intentionally deferred; profile UIs can show accepted document metadata, but cannot fetch superseded document bodies without a future endpoint.

## Related

- ADR-0012: GDPR Primitives Baked Into the Template
- ADR-0017: HybridCache for Data Caching
- ADR-0025: ProblemDetails for All Error Responses
- ADR-0026: IModuleSeeder Contract for Deterministic Seed Data
- ADR-0031: Third-party Authentication

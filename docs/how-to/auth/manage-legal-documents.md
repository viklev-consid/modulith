# Manage Legal Documents

The Users module owns legal document content and acceptance state for Terms of Service and Privacy Policy. The frontend renders Markdown returned by the API and echoes back the document ID, version, and content hash when the user accepts. This keeps the auditable consent artifact tied to the exact text the user saw.

This flow is separate from GDPR consent tracking. Terms acceptance records contractual agreement to a legal document version; consent records optional processing choices such as marketing email opt-in.

## Storage Model

Legal documents are stored in the Users database as `legal_documents` rows. Each row has:

- `DocumentType`: `TermsOfService` or `PrivacyPolicy`
- `Version`: a short wire version such as `1.0`
- `MarkdownContent`: backend-owned Markdown shown to the user
- `ContentHash`: SHA-256 hash of the Markdown
- `IsRequiredForOnboarding`: whether new users must accept it during onboarding
- `IsRequiredForContinuedUse`: whether already-onboarded users must accept it before continuing
- `ContinuedUseRequiredAt`: when the continued-use requirement becomes active; `null` means active immediately
- `BlockingLevel`: currently `None` or `BlockAllAuthenticatedUse`
- `SupersededAt`: set when a version is no longer current

User acceptances are stored as immutable `terms_acceptances` rows keyed by user and version key. They include the accepted document type, version, content hash, accepted timestamp, IP address, and user agent.

## Markdown Sources

Current legal content is backend-first Markdown in:

- `src/Modules/Users/Modulith.Modules.Users/LegalDocuments/terms-of-service.v{version}.md`
- `src/Modules/Users/Modulith.Modules.Users/LegalDocuments/privacy-policy.v{version}.md`

The current versions are configured with:

```json
{
  "Modules": {
    "Users": {
      "TermsOfServiceVersion": "1.0",
      "PrivacyPolicyVersion": "1.0"
    }
  }
}
```

`LegalDocumentsSeeder` reads the embedded Markdown resources at startup and ensures a database row exists for the configured versions. It runs outside the development-only seeder branch because legal content is application data, not sample data. The seeder is idempotent and tolerates a unique-constraint race during multi-replica startup.

When publishing a new version, add a new Markdown file and bump the matching `Modules:Users:*Version` setting. Do not edit the content of an already-published version without changing the version; existing acceptances point at the prior hash.

## Client Flows

### Onboarding

1. The client calls `GET /v1/users/me/onboarding/legal-requirements`.
2. The response contains required current documents with Markdown and `contentHash`.
3. The client renders each document and asks for explicit acceptance.
4. The client calls `POST /v1/users/me/onboarding` with `acceptedDocuments`.
5. The backend verifies every required document is present and the echoed hash matches the current stored hash.

Request shape:

```json
{
  "acceptMarketingEmails": false,
  "acceptedDocuments": [
    {
      "documentId": "00000000-0000-0000-0000-000000000000",
      "version": "1.0",
      "contentHash": "..."
    }
  ]
}
```

The old `acceptTerms` boolean is intentionally gone. A boolean cannot prove which document text the user accepted, and it cannot cover separate Terms of Service and Privacy Policy artifacts safely.

### Continued Use

For already-onboarded users, the client calls `GET /v1/users/me/legal-compliance` to display:

- `acceptedDocuments`: recent accepted document versions for profile/history UI
- `missingDocuments`: current required documents the user has not accepted
- `blockingLevel`: whether missing documents are informational or block use

To accept missing current documents, call `POST /v1/users/me/legal-acceptances` with the same `acceptedDocuments` payload shape as onboarding.

If a missing document has `blockingLevel = "blockAllAuthenticatedUse"`, the global Users legal-compliance middleware returns HTTP 428 `ProblemDetails` for normal authenticated `/v1/*` requests until the user accepts. The 428 response includes `missingDocuments` in extensions so clients can render the re-acceptance prompt without an extra round trip.

The middleware intentionally allows these endpoints while blocked:

- legal compliance and acceptance endpoints
- current legal document GET endpoint
- onboarding legal requirements and onboarding completion
- logout
- GDPR personal-data export
- account deletion
- `GET /v1/users/me`

### Fetching Current Document Content

Use `GET /v1/users/legal-documents/{type}/{version}` when the client has a current accepted or missing document summary and needs Markdown content for the current non-superseded version.

Supported wire types:

- `termsOfService`
- `privacyPolicy`

The endpoint returns `Cache-Control: private, max-age=300` on successful responses. Unknown, missing, or superseded documents return 404 `ProblemDetails` and are not cache-marked by the endpoint.

The endpoint is intentionally not a historical archive. Superseded versions are hidden for now; if historical display becomes a product requirement, add it deliberately with access, caching, and legal retention semantics reviewed.

## Caching And Invalidation

Continued-use compliance is cached per user through HybridCache because the middleware can run for every authenticated `/v1/*` request. The cache is invalidated when:

- the user accepts legal documents
- onboarding writes legal document acceptances
- the legal document seeder publishes a configured document version

The TTL can be longer than most user-facing caches because legal documents change rarely. Keep in mind that multi-replica L1 cache entries may briefly serve stale compliance state if distributed invalidation is delayed.

## Implementation Notes

- Endpoints still follow the normal vertical-slice pattern and dispatch through `IMessageBus`.
- Expected failures return `ErrorOr` failures and are converted to `ProblemDetails`.
- The frontend should never synthesize or hardcode legal document copy.
- The backend should not accept arbitrary document acceptances; only current onboarding or continued-use required documents can be accepted through the public flows.
- GDPR export and account deletion must remain available even when a user is blocked by a new legal document.

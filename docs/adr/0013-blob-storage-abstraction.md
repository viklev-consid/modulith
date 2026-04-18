# ADR-0013: IBlobStore Abstraction with Local-Disk Reference Implementation

## Status

Accepted

## Context

Most applications need to store binary content — user avatars, uploaded documents, generated PDFs, etc. Options:

1. **Store bytes in the database.** Simple, transactional, terrible for anything beyond kilobytes.
2. **Store on the filesystem.** Easy locally, operationally painful once you scale past one host.
3. **Store in cloud blob storage** (S3, Azure Blob, GCS). The production answer, but ties local dev to a cloud account.
4. **Abstract behind an interface.** Local-disk in dev, cloud in prod. The usual right answer — but only if the abstraction is well-shaped.

A badly shaped blob abstraction leaks provider assumptions: folder paths, public URLs, `string`-typed keys. A well-shaped one is stream-based, uses opaque identifiers, and exposes a single method for "get me a URL the client can use to download this."

## Decision

### The abstraction

In `Shared.Infrastructure`:

```csharp
public interface IBlobStore
{
    Task<BlobRef> PutAsync(Stream content, BlobMetadata metadata, CancellationToken ct);
    Task<BlobContent> GetAsync(BlobRef reference, CancellationToken ct);
    Task DeleteAsync(BlobRef reference, CancellationToken ct);
    Task<Uri> GetDownloadUrlAsync(BlobRef reference, TimeSpan lifetime, CancellationToken ct);
}

public sealed record BlobRef(string Container, string Key);
public sealed record BlobMetadata(string ContentType, long Length, string? FileName);
public sealed record BlobContent(Stream Stream, BlobMetadata Metadata);
```

Key design choices:

- **Stream-based.** Not `byte[]`. Avoids loading blobs into memory.
- **Opaque `BlobRef`.** Consumers persist the ref, not a path. Providers own the key format.
- **`GetDownloadUrlAsync`.** Returns a pre-signed URL in cloud implementations; returns a tokenized URL handled by a local controller for the disk implementation. Clients download directly — API never streams blobs through itself.
- **Per-module containers.** Configured in each module's registration (`builder.Services.AddBlobStorage("orders-attachments")`). Isolates namespaces; mirrors the DbContext-per-module decision.

### The local-disk reference implementation

Ships with the template. Designed to mimic cloud semantics:

- GUID-keyed filenames (never user-supplied).
- Sidecar metadata files (`<guid>.meta.json`) so `ContentType`, `Length`, `FileName` survive round trips.
- `GetDownloadUrlAsync` issues a short-lived JWT bound to the blob key; a `LocalBlobDownloadController` validates and streams.
- Storage root configurable per environment.

### Upload handling

The template includes a reference slice that uses `MultipartReader` directly (not `IFormFile`, which buffers). The reader streams the upload into `IBlobStore.PutAsync` without materializing the whole file.

### Validation hook

`IBlobValidator` is a pipeline invoked before `PutAsync` completes. No-op by default. Teams can plug in antivirus scanning, content-type enforcement, max-size checks, etc.

### Lifecycle: two-phase commit

Orphaned blobs are the canonical problem — upload succeeds, downstream DB save fails, blob is stranded forever. Solution:

1. **Upload** completes. Blob exists, `BlobUploaded(BlobRef)` is published via outbox.
2. **Domain operation** references the blob. When the aggregate is saved, `BlobCommitted(BlobRef)` is published.
3. **Orphan sweeper** (scheduled Wolverine job) deletes blobs whose `BlobUploaded` has no corresponding `BlobCommitted` after N hours.

This guarantees no stranded blobs regardless of where the operation fails.

## Deliberately NOT included

- **Resumable uploads.** Provider-specific; document the extension point.
- **CDN integration.** Deployment concern; the `GetDownloadUrlAsync` return type is `Uri`, so CDN-fronted URLs Just Work without code changes.
- **Content-addressed storage.** Deduplication is a specialized concern.
- **Encryption-at-rest beyond what the provider offers.** Documented extension point.

## Consequences

**Positive:**

- Swapping providers is a DI registration change. Azure Blob, S3, GCS all implement the same seam.
- Local dev runs without cloud credentials. The disk implementation is first-class, not a toy.
- The two-phase commit lifecycle prevents the most common correctness issue.
- Streaming uploads avoid the OOM footgun of large-file handling.

**Negative:**

- Teams who just want `File.WriteAllBytes` find the abstraction over-engineered for simple cases. Accepted — the alternative is retrofitting later.
- The tokenized-URL flow for the local implementation requires a controller endpoint. One extra piece of infrastructure to understand.
- Cross-provider URL differences (S3 pre-signed vs. SAS tokens vs. GCS signed URLs) are papered over but not identical (e.g., some support range requests, some don't). Documented caveat.

## Related

- ADR-0003 (Wolverine): the outbox that delivers the upload/commit events.
- ADR-0005 (Module Communication): per-module containers align with per-module boundaries.

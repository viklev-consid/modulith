namespace Modulith.Shared.Infrastructure.Blobs;

public interface IBlobStore
{
    Task<BlobRef> PutAsync(Stream content, BlobMetadata metadata, CancellationToken ct);
    Task<BlobContent> GetAsync(BlobRef reference, CancellationToken ct);
    Task DeleteAsync(BlobRef reference, CancellationToken ct);
    Task<Uri> GetDownloadUrlAsync(BlobRef reference, TimeSpan lifetime, CancellationToken ct);
}

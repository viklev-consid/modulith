namespace Modulith.Shared.Infrastructure.Blobs;

public sealed record BlobContent(Stream Stream, BlobMetadata Metadata);

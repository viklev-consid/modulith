namespace Modulith.Shared.Infrastructure.Blobs;

public sealed class LocalDiskBlobStoreOptions
{
    public string RootPath { get; init; } =
        Path.Combine(Path.GetTempPath(), "modulith-blobs");

    // Must be overridden with a strong random value in production.
    public string SigningKey { get; init; } = "dev-only-signing-key-change-in-production";
}

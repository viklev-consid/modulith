using System.ComponentModel.DataAnnotations;

namespace Modulith.Shared.Infrastructure.Blobs;

public sealed class LocalDiskBlobStoreOptions
{
    public const string DefaultSigningKey = "dev-only-signing-key-change-in-production";

    [Required]
    public string RootPath { get; init; } =
        Path.Combine(Path.GetTempPath(), "modulith-blobs");

    // Must be overridden with a strong random value in production.
    [Required, MinLength(32)]
    public string SigningKey { get; init; } = DefaultSigningKey;
}

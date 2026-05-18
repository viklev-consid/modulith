using Modulith.Modules.Users.Avatars;
using Modulith.Shared.Infrastructure.Blobs;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.IntegrationTests.Fakes;

public sealed class FakeGoogleAvatarImporter(IBlobStore blobStore, IClock clock) : IGoogleAvatarImporter
{
    public int ImportAttempts { get; private set; }

    public async Task<StoredAvatar?> ImportAsync(string? pictureUrl, CancellationToken ct)
    {
        ImportAttempts++;
        if (string.IsNullOrWhiteSpace(pictureUrl))
        {
            return null;
        }

        var bytes = "fake-google-avatar"u8.ToArray();
        await using var stream = new MemoryStream(bytes);
        var blobRef = await blobStore.PutAsync(stream, new BlobMetadata("image/png", bytes.Length, "avatar.png"), ct);
        return new StoredAvatar(blobRef, "image/png", bytes.Length, clock.UtcNow);
    }
}

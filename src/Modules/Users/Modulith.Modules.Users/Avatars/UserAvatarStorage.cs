using Modulith.Shared.Infrastructure.Blobs;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Avatars;

public sealed class UserAvatarStorage(IBlobStore blobStore, IClock clock) : IUserAvatarStorage
{
    public async Task<StoredAvatar> StoreAsync(Stream content, AvatarImageInfo info, CancellationToken ct)
    {
        var metadata = new BlobMetadata(info.ContentType, info.Length, $"avatar{GetExtension(info.ContentType)}");
        var blobRef = await blobStore.PutAsync(content, metadata, ct);
        return new StoredAvatar(blobRef, info.ContentType, info.Length, clock.UtcNow);
    }

    public Task<BlobContent> GetAsync(string container, string key, CancellationToken ct) =>
        blobStore.GetAsync(new BlobRef(container, key), ct);

    public async Task DeleteAsync(string? container, string? key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(container) || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        await blobStore.DeleteAsync(new BlobRef(container, key), ct);
    }

    private static string GetExtension(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".img",
        };
}

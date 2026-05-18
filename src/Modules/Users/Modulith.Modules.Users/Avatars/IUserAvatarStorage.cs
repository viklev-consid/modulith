using Modulith.Shared.Infrastructure.Blobs;

namespace Modulith.Modules.Users.Avatars;

public interface IUserAvatarStorage
{
    Task<StoredAvatar> StoreAsync(Stream content, AvatarImageInfo info, CancellationToken ct);
    Task<BlobContent> GetAsync(string container, string key, CancellationToken ct);
    Task DeleteAsync(string? container, string? key, CancellationToken ct);
}

using ErrorOr;

namespace Modulith.Modules.Users.Avatars;

public interface IAvatarImageInspector
{
    Task<ErrorOr<AvatarImageInfo>> ValidateAsync(
        Stream content,
        string? declaredContentType,
        long length,
        CancellationToken ct);
}

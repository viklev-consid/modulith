using ErrorOr;

namespace Modulith.Modules.Users.Avatars;

public interface IAvatarImageValidator
{
    Task<ErrorOr<AvatarImageInfo>> ValidateAsync(
        Stream content,
        string? declaredContentType,
        long length,
        CancellationToken ct);
}

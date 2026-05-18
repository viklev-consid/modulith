namespace Modulith.Modules.Users.Avatars;

public interface IGoogleAvatarImporter
{
    Task<StoredAvatar?> ImportAsync(string? pictureUrl, CancellationToken ct);
}

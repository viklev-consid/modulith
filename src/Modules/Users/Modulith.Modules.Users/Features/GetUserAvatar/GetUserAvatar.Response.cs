namespace Modulith.Modules.Users.Features.GetUserAvatar;

public sealed record GetUserAvatarResponse(Stream? Content, string? ContentType, DateTimeOffset UpdatedAt, bool NotModified)
{
    public string ETag => ToETag(UpdatedAt);

    public static string ToETag(DateTimeOffset updatedAt) =>
        $"\"{updatedAt.ToUnixTimeMilliseconds():x}\"";
}

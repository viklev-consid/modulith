namespace Modulith.Modules.Users.Avatars;

internal static class AvatarUrl
{
    public static string ForUser(Guid userId, DateTimeOffset updatedAt) =>
        $"/v1/users/{userId}/avatar?v={updatedAt.ToUnixTimeMilliseconds()}";
}

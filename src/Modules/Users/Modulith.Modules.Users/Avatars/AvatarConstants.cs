namespace Modulith.Modules.Users.Avatars;

internal static class AvatarConstants
{
    public const long MaxSizeBytes = 1_048_576;
    public const int MinDimensionPixels = 128;
    public const int MaxDimensionPixels = 512;

    public static readonly IReadOnlySet<string> AllowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };
}

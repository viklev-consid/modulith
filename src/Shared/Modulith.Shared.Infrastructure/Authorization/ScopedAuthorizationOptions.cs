namespace Modulith.Shared.Infrastructure.Authorization;

public sealed record ScopedAuthorizationOptions(bool AllowPlatformOverride)
{
    public static ScopedAuthorizationOptions RequireScopedPermission { get; } = new(false);
    public static ScopedAuthorizationOptions WithPlatformOverride { get; } = new(true);
}

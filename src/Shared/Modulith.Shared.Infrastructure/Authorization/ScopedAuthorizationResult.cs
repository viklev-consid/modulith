namespace Modulith.Shared.Infrastructure.Authorization;

public sealed record ScopedAuthorizationResult(
    bool Succeeded,
    ScopedAuthorizationAccessMode AccessMode)
{
    public static ScopedAuthorizationResult Denied { get; } =
        new(false, ScopedAuthorizationAccessMode.None);

    public static ScopedAuthorizationResult ScopedPermission { get; } =
        new(true, ScopedAuthorizationAccessMode.ScopedPermission);

    public static ScopedAuthorizationResult PlatformOverride { get; } =
        new(true, ScopedAuthorizationAccessMode.PlatformOverride);
}

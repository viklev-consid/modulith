namespace Modulith.Modules.Audit.Contracts.Authorization;

public static class AuditPermissions
{
    public const string TrailRead = "audit.trail.read";

    public static IReadOnlyCollection<string> All { get; } =
        [TrailRead];
}

namespace Modulith.Shared.Infrastructure.Authorization;

/// <summary>
/// Contributes a fixed set of permission constants to the <c>PermissionCatalog</c>.
/// Register one per module in each module's <c>Add*Module</c> extension method via
/// <see cref="PermissionSourceExtensions.AddPermissions"/>.
/// </summary>
public interface IPermissionSource
{
    IReadOnlyCollection<string> GetPermissions();
}

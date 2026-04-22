using Modulith.Modules.Audit.Contracts.Authorization;
using Modulith.Shared.Infrastructure.Authorization;

namespace Modulith.Modules.Audit.Authorization;

/// <summary>
/// Guards access to an audit trail scoped to a specific actor.
/// Callers with <c>audit.trail.read</c> may query any actor's trail.
/// All other authenticated callers may query only their own.
/// </summary>
internal sealed class AuditTrailPolicy : PermissionOrOwnerPolicy<AuditTrailResource>
{
    protected override string ElevatedPermission => AuditPermissions.TrailRead;
    protected override string? GetOwnerId(AuditTrailResource resource) => resource.ActorId.ToString();
}

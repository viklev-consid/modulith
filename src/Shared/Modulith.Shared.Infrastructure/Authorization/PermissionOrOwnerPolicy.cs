using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Shared.Infrastructure.Authorization;

/// <summary>
/// Base class for the common case: elevated permission grants full access;
/// falling back to an ownership check when the permission is absent.
/// </summary>
/// <remarks>
/// Override <see cref="ElevatedPermission"/> with the permission constant that grants
/// unrestricted access (e.g. <c>AuditPermissions.TrailRead</c>) and
/// <see cref="GetOwnerId"/> to extract the owner's ID string from the resource.
/// For more complex rules (multi-tenant membership, delegated access, etc.) implement
/// <see cref="IResourcePolicy{TResource}"/> directly instead.
/// </remarks>
/// <typeparam name="TResource">The resource type being protected.</typeparam>
public abstract class PermissionOrOwnerPolicy<TResource> : IResourcePolicy<TResource>
{
    /// <summary>
    /// The permission constant that grants unrestricted access to all instances of
    /// <typeparamref name="TResource"/>, regardless of ownership.
    /// </summary>
    protected abstract string ElevatedPermission { get; }

    /// <summary>
    /// Extracts the owner's ID string from the resource. Compared against
    /// <see cref="ICurrentUser.Id"/> using ordinal equality.
    /// </summary>
    protected abstract string? GetOwnerId(TResource resource);

    /// <inheritdoc/>
    public bool IsAuthorized(ICurrentUser caller, TResource resource)
        => caller.HasPermission(ElevatedPermission)
           || (caller.Id is not null && string.Equals(caller.Id, GetOwnerId(resource), StringComparison.Ordinal));
}

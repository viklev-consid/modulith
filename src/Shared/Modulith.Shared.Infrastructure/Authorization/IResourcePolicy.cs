using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Shared.Infrastructure.Authorization;

/// <summary>
/// Determines whether the current caller may access a specific resource instance.
/// Implement this interface in each module for resources that require ownership-aware
/// authorization — i.e. where elevated permissions grant broad access but authenticated
/// users may still access their own data.
/// </summary>
/// <typeparam name="TResource">
/// The resource type being protected. Can be a domain entity or a lightweight scope
/// record (e.g. <c>AuditTrailResource(Guid ActorId)</c>) when protecting a collection
/// query rather than a single entity.
/// </typeparam>
public interface IResourcePolicy<in TResource>
{
    /// <summary>
    /// Returns <c>true</c> if <paramref name="caller"/> is authorized to access
    /// <paramref name="resource"/>; otherwise <c>false</c>.
    /// </summary>
    bool IsAuthorized(ICurrentUser caller, TResource resource);
}

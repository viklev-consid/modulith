namespace Modulith.Shared.Kernel.Interfaces;

public interface ICurrentUser
{
    string? Id { get; }
    string? Name { get; }
    bool IsAuthenticated { get; }

    /// <summary>
    /// The ID of the refresh token that accompanied the access token on this request.
    /// Populated from the <c>rtid</c> JWT claim. Null when not authenticated via a
    /// token that carried a refresh-token ID (e.g., machine-to-machine or legacy tokens).
    /// Used by ChangePassword to preserve the caller's own session while revoking others.
    /// </summary>
    string? ActiveRefreshTokenId { get; }

    /// <summary>The caller's current role name (e.g. <c>"admin"</c>, <c>"user"</c>).</summary>
    string? Role { get; }

    /// <summary>
    /// The fully resolved permission set for the caller's role, injected per-request by
    /// <c>PermissionClaimsTransformation</c>. Empty for unauthenticated callers or roles
    /// with no permissions.
    /// </summary>
    IReadOnlyCollection<string> Permissions { get; }

    /// <summary>Returns <c>true</c> if the caller holds the specified permission.</summary>
    bool HasPermission(string permission);
}

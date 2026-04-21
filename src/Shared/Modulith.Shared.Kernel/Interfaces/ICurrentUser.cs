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
}

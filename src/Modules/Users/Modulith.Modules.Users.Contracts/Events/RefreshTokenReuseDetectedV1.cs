namespace Modulith.Modules.Users.Contracts.Events;

/// <summary>
/// Published when a rotated (already-consumed) refresh token is presented again.
/// This indicates the original token was stolen. The active session chain has been
/// revoked and the affected user must re-authenticate.
/// </summary>
public sealed record RefreshTokenReuseDetectedV1(Guid UserId, Guid EventId);

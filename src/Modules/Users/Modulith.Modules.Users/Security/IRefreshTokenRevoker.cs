using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Security;

// Public so that Wolverine handler constructors (which must be public) can reference this type.
// Being public inside an internal project doesn't expose it outside the module.
public interface IRefreshTokenRevoker
{
    /// <summary>
    /// Revokes all active refresh tokens for the user. Executes immediately via a bulk
    /// UPDATE — does not go through EF change tracking. Caller is responsible for ordering
    /// relative to SaveChangesAsync when transactional isolation matters.
    /// </summary>
    Task RevokeAllForUserAsync(UserId userId, CancellationToken ct, RefreshTokenId? except = null);
}

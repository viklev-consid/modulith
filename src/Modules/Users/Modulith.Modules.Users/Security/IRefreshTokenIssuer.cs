using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Security;

// Public so that Wolverine handler constructors (which must be public) can reference this type.
// Being public inside an internal project doesn't expose it outside the module.
public interface IRefreshTokenIssuer
{
    /// <summary>
    /// Issues a new refresh token for the user. Enforces MaxActiveRefreshTokensPerUser
    /// by revoking the oldest active token if the limit is reached. Adds the new token
    /// to the DbContext — the caller is responsible for calling SaveChangesAsync.
    /// </summary>
    Task<(RefreshToken token, string rawValue)> IssueAsync(UserId userId, CancellationToken ct);
}

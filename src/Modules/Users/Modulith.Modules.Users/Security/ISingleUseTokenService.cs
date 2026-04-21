using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Security;

// Public so that Wolverine handler constructors (which must be public) can reference this type.
// Being public inside an internal project doesn't expose it outside the module.
public interface ISingleUseTokenService
{
    /// <summary>
    /// Creates a new single-use token for the specified purpose. Adds the token to the
    /// DbContext — the caller is responsible for calling SaveChangesAsync.
    /// Returns the entity and the raw token value (to send to the user).
    /// </summary>
    (SingleUseToken token, string rawValue) Create(UserId userId, TokenPurpose purpose, TimeSpan lifetime);

    /// <summary>
    /// Looks up an active (not consumed, not expired) token by its raw value and purpose.
    /// Returns null if not found or invalid.
    /// </summary>
    Task<SingleUseToken?> FindValidAsync(string rawToken, TokenPurpose purpose, CancellationToken ct);
}

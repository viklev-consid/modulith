using ErrorOr;

namespace Modulith.Modules.Users.Security;

public interface IGoogleIdTokenVerifier
{
    Task<ErrorOr<GoogleIdentity>> VerifyAsync(string idToken, CancellationToken ct = default);
}

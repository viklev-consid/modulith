using ErrorOr;

namespace Modulith.Modules.Users.Security;

internal interface IGoogleIdTokenVerifier
{
    Task<ErrorOr<GoogleIdentity>> VerifyAsync(string idToken, CancellationToken ct = default);
}

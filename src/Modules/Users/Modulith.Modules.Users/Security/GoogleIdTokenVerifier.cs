using ErrorOr;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Modulith.Modules.Users.Errors;

namespace Modulith.Modules.Users.Security;

internal sealed class GoogleIdTokenVerifier(
    HttpClient http,
    IMemoryCache cache,
    IOptions<GoogleAuthOptions> options) : IGoogleIdTokenVerifier
{
    private static readonly JsonWebTokenHandler TokenHandler = new() { SetDefaultTimesOnTokenCreation = false };

    public async Task<ErrorOr<GoogleIdentity>> VerifyAsync(string idToken, CancellationToken ct = default)
    {
        var opts = options.Value;
        var keys = await GetSigningKeysAsync(opts, forceRefresh: false, ct);
        if (keys is null)
        {
            return UsersErrors.ExternalAuthUnavailable;
        }

        var result = await ValidateAsync(idToken, opts, keys);

        // Cached JWKS may be stale after a Google key rotation. On a kid miss,
        // force one refresh and retry before failing closed.
        if (!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            keys = await GetSigningKeysAsync(opts, forceRefresh: true, ct);
            if (keys is null)
            {
                return UsersErrors.ExternalAuthUnavailable;
            }
            result = await ValidateAsync(idToken, opts, keys);
        }

        if (!result.IsValid)
        {
            return UsersErrors.InvalidIdToken;
        }

        var sub = result.ClaimsIdentity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var email = result.ClaimsIdentity.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        var emailVerified = result.ClaimsIdentity.FindFirst("email_verified")?.Value;
        var name = result.ClaimsIdentity.FindFirst("name")?.Value;

        if (string.IsNullOrEmpty(sub) || string.IsNullOrEmpty(email))
        {
            return UsersErrors.InvalidIdToken;
        }

        if (!string.Equals(emailVerified, "true", StringComparison.OrdinalIgnoreCase))
        {
            return UsersErrors.InvalidIdToken;
        }

        return new GoogleIdentity(sub, email, name ?? email);
    }

    private static Task<TokenValidationResult> ValidateAsync(string idToken, GoogleAuthOptions opts, IEnumerable<SecurityKey> keys) =>
        TokenHandler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuers = ["accounts.google.com", "https://accounts.google.com"],
            ValidAudience = opts.ClientId,
            IssuerSigningKeys = keys,
            ValidateLifetime = true,
        });

    private async Task<IEnumerable<SecurityKey>?> GetSigningKeysAsync(GoogleAuthOptions opts, bool forceRefresh, CancellationToken ct)
    {
        const string cacheKey = "users:google:jwks";
        if (!forceRefresh && cache.TryGetValue(cacheKey, out IEnumerable<SecurityKey>? cached))
        {
            return cached;
        }

        try
        {
            var json = await http.GetStringAsync(opts.JwksUri, ct);
            var jwks = new JsonWebKeySet(json);
            var keys = jwks.GetSigningKeys();
            cache.Set(cacheKey, keys, opts.JwksCacheDuration);
            return keys;
        }
        catch
        {
            return null;
        }
    }
}

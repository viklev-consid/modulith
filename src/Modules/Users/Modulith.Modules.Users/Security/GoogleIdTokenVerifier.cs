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
        var keys = await GetSigningKeysAsync(opts, ct);
        if (keys is null)
        {
            return UsersErrors.ExternalAuthUnavailable;
        }

        var result = await TokenHandler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuers = ["accounts.google.com", "https://accounts.google.com"],
            ValidAudience = opts.ClientId,
            IssuerSigningKeys = keys,
            ValidateLifetime = true,
        });

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

    private async Task<IEnumerable<SecurityKey>?> GetSigningKeysAsync(GoogleAuthOptions opts, CancellationToken ct)
    {
        const string cacheKey = "users:google:jwks";
        if (cache.TryGetValue(cacheKey, out IEnumerable<SecurityKey>? cached))
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

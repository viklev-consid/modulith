using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Modulith.Modules.Users;
using Modulith.Modules.Users.Security;

namespace Modulith.Modules.Users.UnitTests.Security;

[Trait("Category", "Unit")]
public sealed class GoogleIdTokenVerifierTests : IDisposable
{
    private const string clientId = "test-client-id";
    private const string validSubject = "109742855438971236270";
    private const string validEmail = "alice@example.com";
    private const string validName = "Alice";

    private readonly RSA rsa;
    private readonly RsaSecurityKey signingKey;
    private readonly string jwksJson;

    public GoogleIdTokenVerifierTests()
    {
        rsa = RSA.Create(2048);
        signingKey = new RsaSecurityKey(rsa) { KeyId = "test-key-id" };

        var p = rsa.ExportParameters(includePrivateParameters: false);
        var n = Base64UrlEncoder.Encode(p.Modulus!);
        var e = Base64UrlEncoder.Encode(p.Exponent!);
        jwksJson = $$"""{"keys":[{"kty":"RSA","kid":"test-key-id","use":"sig","n":"{{n}}","e":"{{e}}"}]}""";
    }

    public void Dispose() => rsa.Dispose();

    [Fact]
    public async Task VerifyAsync_WithEmailVerifiedTrue_ReturnsGoogleIdentity()
    {
        var verifier = CreateVerifier();
        var token = CreateToken(emailVerified: "true");

        var result = await verifier.VerifyAsync(token);

        Assert.False(result.IsError);
        Assert.Equal(validSubject, result.Value.Subject);
        Assert.Equal(validEmail, result.Value.Email);
    }

    [Fact]
    public async Task VerifyAsync_WithEmailVerifiedFalse_ReturnsInvalidIdTokenError()
    {
        var verifier = CreateVerifier();
        var token = CreateToken(emailVerified: "false");

        var result = await verifier.VerifyAsync(token);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task VerifyAsync_WithEmailVerifiedClaimAbsent_ReturnsInvalidIdTokenError()
    {
        var verifier = CreateVerifier();
        var token = CreateToken(emailVerified: null);

        var result = await verifier.VerifyAsync(token);

        Assert.True(result.IsError);
    }

    private GoogleIdTokenVerifier CreateVerifier()
    {
        var httpClient = new HttpClient(new StubJwksMessageHandler(jwksJson));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var opts = Options.Create(new GoogleAuthOptions
        {
            ClientId = clientId,
            JwksUri = "https://www.googleapis.com/oauth2/v3/certs",
        });
        return new GoogleIdTokenVerifier(httpClient, cache, opts);
    }

    private string CreateToken(string? emailVerified)
    {
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, validSubject),
            new(JwtRegisteredClaimNames.Email, validEmail),
            new("name", validName),
        };

        if (emailVerified is not null)
        {
            claims.Add(new("email_verified", emailVerified));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "accounts.google.com",
            Audience = clientId,
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = credentials,
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private sealed class StubJwksMessageHandler(string jwksJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jwksJson),
            });
    }
}

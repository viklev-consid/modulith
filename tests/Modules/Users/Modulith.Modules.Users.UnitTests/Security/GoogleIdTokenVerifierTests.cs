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
    private const string ClientId = "test-client-id";
    private const string ValidSubject = "109742855438971236270";
    private const string ValidEmail = "alice@example.com";
    private const string ValidName = "Alice";

    private readonly RSA _rsa;
    private readonly RsaSecurityKey _signingKey;
    private readonly string _jwksJson;

    public GoogleIdTokenVerifierTests()
    {
        _rsa = RSA.Create(2048);
        _signingKey = new RsaSecurityKey(_rsa) { KeyId = "test-key-id" };

        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(_signingKey);
        var jwks = new JsonWebKeySet();
        jwks.Keys.Add(jwk);
        _jwksJson = jwks.ToString() ?? throw new InvalidOperationException("Failed to serialize JWKS.");
    }

    public void Dispose() => _rsa.Dispose();

    [Fact]
    public async Task VerifyAsync_WithEmailVerifiedTrue_ReturnsGoogleIdentity()
    {
        var verifier = CreateVerifier();
        var token = CreateToken(emailVerified: "true");

        var result = await verifier.VerifyAsync(token);

        Assert.False(result.IsError);
        Assert.Equal(ValidSubject, result.Value.Subject);
        Assert.Equal(ValidEmail, result.Value.Email);
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
        var httpClient = new HttpClient(new StubJwksMessageHandler(_jwksJson));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var opts = Options.Create(new GoogleAuthOptions
        {
            ClientId = ClientId,
            JwksUri = "https://www.googleapis.com/oauth2/v3/certs",
        });
        return new GoogleIdTokenVerifier(httpClient, cache, opts);
    }

    private string CreateToken(string? emailVerified)
    {
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, ValidSubject),
            new(JwtRegisteredClaimNames.Email, ValidEmail),
            new("name", ValidName),
        };

        if (emailVerified is not null)
        {
            claims.Add(new("email_verified", emailVerified));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "accounts.google.com",
            Audience = ClientId,
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

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Security;

internal sealed class JwtGenerator(IOptions<UsersOptions> options) : IJwtGenerator
{
    private readonly UsersOptions _options = options.Value;

    public string Generate(UserId userId, string email, string displayName)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.Value.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Name, displayName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _options.JwtIssuer,
            audience: _options.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.JwtExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

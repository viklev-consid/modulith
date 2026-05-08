using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Modulith.Modules.Users.Domain;
using Modulith.Shared.Infrastructure.Auth;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Security;

internal sealed class JwtGenerator(
    IOptions<JwtOptions> jwtOptions,
    IOptions<UsersOptions> usersOptions,
    IClock clock) : IJwtGenerator
{
    private readonly JwtOptions jwt = jwtOptions.Value;
    private readonly UsersOptions users = usersOptions.Value;

    public string Generate(UserId userId, string email, string displayName, string role, Guid refreshTokenId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = clock.UtcNow.UtcDateTime;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.Value.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Name, displayName),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("rtid", refreshTokenId.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(users.AccessTokenLifetimeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

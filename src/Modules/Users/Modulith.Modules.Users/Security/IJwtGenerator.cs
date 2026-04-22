using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Security;

public interface IJwtGenerator
{
    string Generate(UserId userId, string email, string displayName, string role, Guid refreshTokenId);
}

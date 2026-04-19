using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Security;

internal interface IJwtGenerator
{
    string Generate(UserId userId, string email, string displayName);
}

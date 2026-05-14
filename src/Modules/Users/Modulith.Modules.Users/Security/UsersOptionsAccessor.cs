using Microsoft.Extensions.Options;

namespace Modulith.Modules.Users.Security;

internal sealed class UsersOptionsAccessor(IOptions<UsersOptions> options)
{
    public UsersOptions Value => options.Value;
}

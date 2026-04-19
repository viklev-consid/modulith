namespace Modulith.Modules.Users.Security;

internal interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

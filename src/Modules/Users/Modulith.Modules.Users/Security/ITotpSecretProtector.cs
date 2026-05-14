namespace Modulith.Modules.Users.Security;

public interface ITotpSecretProtector
{
    string Protect(string secret);
    string Unprotect(string protectedSecret);
}

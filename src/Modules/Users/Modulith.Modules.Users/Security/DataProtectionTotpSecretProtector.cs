using Microsoft.AspNetCore.DataProtection;

namespace Modulith.Modules.Users.Security;

internal sealed class DataProtectionTotpSecretProtector(IDataProtectionProvider provider) : ITotpSecretProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("Modulith.Users.TotpSecret.v1");

    public string Protect(string secret) => protector.Protect(secret);

    public string Unprotect(string protectedSecret) => protector.Unprotect(protectedSecret);
}

namespace Modulith.Modules.Users.Security;

public interface ITotpService
{
    string GenerateSecret();
    string BuildOtpAuthUri(string issuer, string accountName, string secret);
    TotpVerificationResult Verify(string secret, string code, int allowedTimeStepDrift);
}

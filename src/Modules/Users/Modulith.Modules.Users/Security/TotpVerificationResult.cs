namespace Modulith.Modules.Users.Security;

public sealed record TotpVerificationResult(bool IsValid, long TimeStep);

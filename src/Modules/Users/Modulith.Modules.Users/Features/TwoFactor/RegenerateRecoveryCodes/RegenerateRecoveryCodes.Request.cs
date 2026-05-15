namespace Modulith.Modules.Users.Features.TwoFactor.RegenerateRecoveryCodes;

public sealed record RegenerateRecoveryCodesRequest(string CurrentPassword, string Code);

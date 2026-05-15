namespace Modulith.Modules.Users.Features.TwoFactor.RegenerateRecoveryCodes;

public sealed record RegenerateRecoveryCodesCommand(Guid UserId, string CurrentPassword, string Code);

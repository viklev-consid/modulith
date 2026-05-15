namespace Modulith.Modules.Users.Features.TwoFactor.ConfirmTotp;

public sealed record ConfirmTotpResponse(IReadOnlyList<string> RecoveryCodes);

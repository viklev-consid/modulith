namespace Modulith.Modules.Users.Features.TwoFactor.ConfirmTotp;

public sealed record ConfirmTotpCommand(Guid UserId, string Code, string? ActiveRefreshTokenId);

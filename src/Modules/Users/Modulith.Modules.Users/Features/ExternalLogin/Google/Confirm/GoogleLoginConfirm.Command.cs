namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Confirm;

public sealed record GoogleLoginConfirmCommand(string Token, string? InvitationToken, string? IpAddress, string? UserAgent);

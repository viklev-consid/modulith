namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Confirm;

public sealed record GoogleLoginConfirmRequest(string Token, string? InvitationToken = null, bool UseGoogleAvatar = false);

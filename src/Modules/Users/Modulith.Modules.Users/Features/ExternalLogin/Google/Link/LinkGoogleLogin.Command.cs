namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Link;

public sealed record LinkGoogleLoginCommand(Guid UserId, string IdToken, string? IpAddress);

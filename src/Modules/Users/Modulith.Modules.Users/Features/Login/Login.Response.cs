namespace Modulith.Modules.Users.Features.Login;

public sealed record LoginResponse(Guid UserId, string AccessToken);

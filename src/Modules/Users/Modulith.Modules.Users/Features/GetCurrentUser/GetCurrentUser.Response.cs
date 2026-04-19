namespace Modulith.Modules.Users.Features.GetCurrentUser;

public sealed record GetCurrentUserResponse(Guid UserId, string Email, string DisplayName, DateTimeOffset CreatedAt);

namespace Modulith.Modules.Users.Features.RequestEmailChange;

public sealed record RequestEmailChangeCommand(Guid UserId, string NewEmail, string CurrentPassword);

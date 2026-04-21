namespace Modulith.Modules.Users.Features.RequestEmailChange;

public sealed record RequestEmailChangeRequest(string NewEmail, string CurrentPassword);

namespace Modulith.Modules.Users.Contracts.Events;

public sealed record UserEmailChangedV1(Guid UserId, string OldEmail, string NewEmail);

namespace Modulith.Modules.Users.Contracts.Events;

public sealed record UserRoleChangedV1(
    Guid UserId,
    string OldRole,
    string NewRole,
    Guid ChangedBy);

namespace Modulith.Modules.Users.Contracts.Events;

/// <summary>
/// Published after an email change is confirmed.
/// Carries both addresses so Notifications can alert the old address.
/// </summary>
public sealed record EmailChangedV1(Guid UserId, string OldEmail, string NewEmail);

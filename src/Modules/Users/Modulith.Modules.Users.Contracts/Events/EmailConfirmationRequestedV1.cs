namespace Modulith.Modules.Users.Contracts.Events;

/// <summary>Published when a password registration needs email ownership confirmation.</summary>
public sealed record EmailConfirmationRequestedV1(
    Guid UserId,
    string Email,
    string DisplayName,
    string RawToken,
    Guid EventId);

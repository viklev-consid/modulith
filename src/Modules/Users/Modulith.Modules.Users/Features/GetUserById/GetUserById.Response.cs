namespace Modulith.Modules.Users.Features.GetUserById;

public sealed record GetUserByIdResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset CreatedAt,
    bool HasPassword,
    bool HasCompletedOnboarding,
    IReadOnlyCollection<string> LinkedProviders);

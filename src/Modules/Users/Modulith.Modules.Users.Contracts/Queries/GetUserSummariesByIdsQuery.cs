namespace Modulith.Modules.Users.Contracts.Queries;

public sealed record GetUserSummariesByIdsQuery(IReadOnlyCollection<Guid> UserIds);

public sealed record GetUserSummariesByIdsResponse(IReadOnlyCollection<UserSummary> Users);

public sealed record UserSummary(Guid UserId, string Email, string DisplayName);

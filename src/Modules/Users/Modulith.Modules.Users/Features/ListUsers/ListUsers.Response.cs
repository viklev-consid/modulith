namespace Modulith.Modules.Users.Features.ListUsers;

public sealed record ListUsersUserDto(Guid UserId, string Email, string DisplayName, string Role);

public sealed record ListUsersResponse(
    IReadOnlyCollection<ListUsersUserDto> Users,
    int Page,
    int PageSize,
    int TotalCount);

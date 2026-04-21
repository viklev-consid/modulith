using System.ComponentModel.DataAnnotations;

namespace Modulith.Modules.Users;

public sealed class UsersOptions
{
    [Range(1, 1440)]
    public int AccessTokenLifetimeMinutes { get; init; } = 15;

    [Range(1, 365)]
    public int RefreshTokenLifetimeDays { get; init; } = 30;

    [Range(1, 100)]
    public int MaxActiveRefreshTokensPerUser { get; init; } = 10;

    public TimeSpan PasswordResetTokenLifetime { get; init; } = TimeSpan.FromMinutes(30);

    public TimeSpan EmailChangeTokenLifetime { get; init; } = TimeSpan.FromMinutes(30);

    [Range(8, 128)]
    public int MinPasswordLength { get; init; } = 10;
}

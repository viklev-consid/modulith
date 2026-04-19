using System.ComponentModel.DataAnnotations;

namespace Modulith.Modules.Users;

public sealed class UsersOptions
{
    [Required]
    public required string JwtIssuer { get; init; }

    [Required]
    public required string JwtAudience { get; init; }

    [Required, MinLength(32)]
    public required string JwtKey { get; init; }

    [Range(1, 10080)]
    public int JwtExpirationMinutes { get; init; } = 60;
}

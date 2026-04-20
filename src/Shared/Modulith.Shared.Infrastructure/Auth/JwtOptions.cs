using System.ComponentModel.DataAnnotations;

namespace Modulith.Shared.Infrastructure.Auth;

public sealed class JwtOptions
{
    [Required]
    public required string Issuer { get; init; }

    [Required]
    public required string Audience { get; init; }

    [Required, MinLength(32)]
    public required string SigningKey { get; init; }

    [Range(1, 1440)]
    public int AccessTokenLifetimeMinutes { get; init; } = 60;
}

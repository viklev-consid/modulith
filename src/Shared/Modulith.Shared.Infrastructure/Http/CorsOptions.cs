using System.ComponentModel.DataAnnotations;

namespace Modulith.Shared.Infrastructure.Http;

public sealed class CorsOptions
{
    [Required]
    public string PolicyName { get; init; } = "BrowserClients";

    public string[] AllowedOrigins { get; init; } = [];

    public bool AllowCredentials { get; init; }
}


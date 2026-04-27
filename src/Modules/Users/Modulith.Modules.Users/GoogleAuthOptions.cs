using System.ComponentModel.DataAnnotations;

namespace Modulith.Modules.Users;

public sealed class GoogleAuthOptions
{
    [Required]
    public string ClientId { get; init; } = string.Empty;

    public string JwksUri { get; init; } = "https://www.googleapis.com/oauth2/v3/certs";

    public TimeSpan JwksCacheDuration { get; init; } = TimeSpan.FromMinutes(60);
}

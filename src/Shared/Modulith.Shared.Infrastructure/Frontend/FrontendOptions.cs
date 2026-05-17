using System.ComponentModel.DataAnnotations;

namespace Modulith.Shared.Infrastructure.Frontend;

public sealed class FrontendOptions
{
    [Required]
    public string BaseUrl { get; init; } = string.Empty;

    [Required]
    public FrontendPathOptions Paths { get; init; } = new();
}

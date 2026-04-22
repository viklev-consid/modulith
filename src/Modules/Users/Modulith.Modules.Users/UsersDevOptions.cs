using System.ComponentModel.DataAnnotations;

namespace Modulith.Modules.Users;

/// <summary>Dev-only seeder configuration for the Users module.</summary>
public sealed class UsersDevOptions
{
    [EmailAddress]
    public string AdminEmail { get; init; } = "admin@example.test";

    [StringLength(100, MinimumLength = 1)]
    public string AdminDisplayName { get; init; } = "Admin";
}

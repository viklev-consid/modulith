using System.ComponentModel.DataAnnotations;

namespace Modulith.Modules.Users;

/// <summary>Options for the non-dev admin bootstrap hosted service.</summary>
public sealed class AdminBootstrapOptions
{
    /// <summary>
    /// When <c>true</c>, the <c>AdminBootstrapper</c> hosted service runs at startup and
    /// promotes the user with <see cref="Email"/> to the Admin role if no admin exists yet.
    /// Default: <c>false</c>.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Email of the user to promote. Required when <see cref="Enabled"/> is <c>true</c>.
    /// </summary>
    [EmailAddress]
    public string? Email { get; init; }
}

namespace Modulith.Shared.Infrastructure.Notifications;

public sealed class SmtpOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1025;
    public bool UseSsl { get; init; }
    public string DefaultFrom { get; init; } = "noreply@localhost";
    public string? Username { get; init; }
    public string? Password { get; init; }
}

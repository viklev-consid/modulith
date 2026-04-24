using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain;

public sealed class ExternalLogin : Entity<ExternalLoginId>
{
    private ExternalLogin(
        ExternalLoginId id,
        UserId userId,
        ExternalLoginProvider provider,
        string subject,
        DateTimeOffset linkedAt) : base(id)
    {
        UserId = userId;
        Provider = provider;
        Subject = subject;
        LinkedAt = linkedAt;
    }

    // Required by EF Core for materialization.
    private ExternalLogin() : base(new ExternalLoginId(Guid.Empty)) { }

    public UserId UserId { get; private set; } = null!;
    public ExternalLoginProvider Provider { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public DateTimeOffset LinkedAt { get; private set; }

    internal static ExternalLogin Create(
        UserId userId,
        ExternalLoginProvider provider,
        string subject,
        DateTimeOffset linkedAt)
        => new(ExternalLoginId.New(), userId, provider, subject, linkedAt);
}

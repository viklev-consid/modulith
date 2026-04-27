using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain;

public sealed class TermsAcceptance : Entity<TermsAcceptanceId>
{
    private const int MaxUserAgentLength = 512;

    private TermsAcceptance(
        TermsAcceptanceId id,
        UserId userId,
        string version,
        DateTimeOffset acceptedAt,
        string? acceptedFromIp,
        string? userAgent) : base(id)
    {
        UserId = userId;
        Version = version;
        AcceptedAt = acceptedAt;
        AcceptedFromIp = acceptedFromIp;
        UserAgent = userAgent;
    }

    // Required by EF Core for materialization.
    private TermsAcceptance() : base(new TermsAcceptanceId(Guid.Empty)) { }

    public UserId UserId { get; private set; } = null!;
    public string Version { get; private set; } = string.Empty;
    public DateTimeOffset AcceptedAt { get; private set; }
    public string? AcceptedFromIp { get; private set; }
    public string? UserAgent { get; private set; }

    /// <summary>
    /// Records a ToS acceptance. This is a contract-agreement artefact (GDPR Art. 6(1)(b)),
    /// distinct from marketing consent in the Consents table (Art. 6(1)(a)).
    /// </summary>
    public static TermsAcceptance Record(
        UserId userId,
        string version,
        DateTimeOffset acceptedAt,
        string? acceptedFromIp,
        string? userAgent)
    {
        var ua = userAgent?.Length > MaxUserAgentLength ? userAgent[..MaxUserAgentLength] : userAgent;
        return new(TermsAcceptanceId.New(), userId, version, acceptedAt, acceptedFromIp, ua);
    }
}

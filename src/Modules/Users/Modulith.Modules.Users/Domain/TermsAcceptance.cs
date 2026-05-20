using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain;

public sealed class TermsAcceptance : Entity<TermsAcceptanceId>
{
    private const int maxUserAgentLength = 512;

    private TermsAcceptance(
        TermsAcceptanceId id,
        UserId userId,
        string version,
        LegalDocumentType? documentType,
        LegalDocumentId? legalDocumentId,
        string? contentHash,
        DateTimeOffset acceptedAt,
        string? acceptedFromIp,
        string? userAgent) : base(id)
    {
        UserId = userId;
        Version = version;
        DocumentType = documentType;
        LegalDocumentId = legalDocumentId;
        ContentHash = contentHash;
        AcceptedAt = acceptedAt;
        AcceptedFromIp = acceptedFromIp;
        UserAgent = userAgent;
    }

    // Required by EF Core for materialization.
    private TermsAcceptance() : base(new TermsAcceptanceId(Guid.Empty)) { }

    public UserId UserId { get; private set; } = null!;
    public string Version { get; private set; } = string.Empty;
    public LegalDocumentType? DocumentType { get; private set; }
    public LegalDocumentId? LegalDocumentId { get; private set; }
    public string? ContentHash { get; private set; }
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
        var ua = userAgent?.Length > maxUserAgentLength ? userAgent[..maxUserAgentLength] : userAgent;
        return new(TermsAcceptanceId.New(), userId, version, null, null, null, acceptedAt, acceptedFromIp, ua);
    }

    public static TermsAcceptance Record(
        UserId userId,
        LegalDocument legalDocument,
        DateTimeOffset acceptedAt,
        string? acceptedFromIp,
        string? userAgent)
    {
        var ua = userAgent?.Length > maxUserAgentLength ? userAgent[..maxUserAgentLength] : userAgent;
        var version = $"{LegalDocumentKeys.GetPrefix(legalDocument.DocumentType)}:{legalDocument.Version}";
        return new(
            TermsAcceptanceId.New(),
            userId,
            version,
            legalDocument.DocumentType,
            legalDocument.Id,
            legalDocument.ContentHash,
            acceptedAt,
            acceptedFromIp,
            ua);
    }
}

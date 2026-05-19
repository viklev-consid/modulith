using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain;

public sealed class LegalDocument : Entity<LegalDocumentId>
{
    private LegalDocument(
        LegalDocumentId id,
        LegalDocumentType documentType,
        string version,
        string title,
        string markdownContent,
        string contentHash,
        DateTimeOffset effectiveAt,
        DateTimeOffset publishedAt,
        bool isRequiredForOnboarding) : base(id)
    {
        DocumentType = documentType;
        Version = version;
        Title = title;
        MarkdownContent = markdownContent;
        ContentHash = contentHash;
        EffectiveAt = effectiveAt;
        PublishedAt = publishedAt;
        IsRequiredForOnboarding = isRequiredForOnboarding;
    }

    // Required by EF Core for materialization.
    private LegalDocument() : base(new LegalDocumentId(Guid.Empty)) { }

    public LegalDocumentType DocumentType { get; private set; }
    public string Version { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string MarkdownContent { get; private set; } = string.Empty;
    public string ContentHash { get; private set; } = string.Empty;
    public DateTimeOffset EffectiveAt { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }
    public DateTimeOffset? SupersededAt { get; private set; }
    public bool IsRequiredForOnboarding { get; private set; }

    public static LegalDocument Publish(
        LegalDocumentType documentType,
        string version,
        string title,
        string markdownContent,
        string contentHash,
        DateTimeOffset effectiveAt,
        DateTimeOffset publishedAt,
        bool isRequiredForOnboarding)
        => new(
            LegalDocumentId.New(),
            documentType,
            version,
            title,
            markdownContent,
            contentHash,
            effectiveAt,
            publishedAt,
            isRequiredForOnboarding);

    public void Supersede(DateTimeOffset supersededAt)
    {
        SupersededAt ??= supersededAt;
    }
}

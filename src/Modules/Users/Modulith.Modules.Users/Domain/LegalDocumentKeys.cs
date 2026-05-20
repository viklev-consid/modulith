namespace Modulith.Modules.Users.Domain;

public static class LegalDocumentKeys
{
    public static string GetPrefix(LegalDocumentType documentType) =>
        documentType switch
        {
            LegalDocumentType.TermsOfService => "tos",
            LegalDocumentType.PrivacyPolicy => "privacy",
            _ => throw new ArgumentOutOfRangeException(nameof(documentType), documentType, "Unknown legal document type."),
        };
}

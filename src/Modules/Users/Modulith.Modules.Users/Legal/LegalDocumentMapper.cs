using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Legal;

public static class LegalDocumentMapper
{
    public static string ToWireType(LegalDocumentType documentType) =>
        documentType switch
        {
            LegalDocumentType.TermsOfService => "termsOfService",
            LegalDocumentType.PrivacyPolicy => "privacyPolicy",
            _ => throw new ArgumentOutOfRangeException(nameof(documentType), documentType, "Unknown legal document type."),
        };

    public static bool TryFromWireType(string type, out LegalDocumentType documentType)
    {
        switch (type)
        {
            case "termsOfService":
                documentType = LegalDocumentType.TermsOfService;
                return true;
            case "privacyPolicy":
                documentType = LegalDocumentType.PrivacyPolicy;
                return true;
            default:
                documentType = default;
                return false;
        }
    }

    public static string ToWireBlockingLevel(LegalDocumentBlockingLevel blockingLevel) =>
        blockingLevel switch
        {
            LegalDocumentBlockingLevel.None => "none",
            LegalDocumentBlockingLevel.BlockAllAuthenticatedUse => "blockAllAuthenticatedUse",
            _ => throw new ArgumentOutOfRangeException(nameof(blockingLevel), blockingLevel, "Unknown legal document blocking level."),
        };
}

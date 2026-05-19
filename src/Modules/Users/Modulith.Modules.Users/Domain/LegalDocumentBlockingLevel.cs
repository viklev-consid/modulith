namespace Modulith.Modules.Users.Domain;

public enum LegalDocumentBlockingLevel
{
    None,
    PromptOnly,
    BlockSensitiveActions,
    BlockAllAuthenticatedUse,
}

using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Features.GetLegalDocument;

public sealed record GetLegalDocumentQuery(LegalDocumentType DocumentType, string Version);

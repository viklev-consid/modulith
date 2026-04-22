using ErrorOr;

namespace Modulith.Modules.Audit.Errors;

internal static class AuditErrors
{
    // Authorization
    public static readonly Error Forbidden =
        Error.Forbidden("Audit.Forbidden", "You do not have permission to access this audit trail.");

    // Pagination
    public static readonly Error PageInvalid =
        Error.Validation("Audit.Query.PageInvalid", "Page number must be greater than zero.");

    public static readonly Error PageSizeInvalid =
        Error.Validation("Audit.Query.PageSizeInvalid", "Page size must be between 1 and 100.");

    // Entry lookup
    public static readonly Error EntryNotFound =
        Error.NotFound("Audit.Entry.NotFound", "Audit entry was not found.");
}

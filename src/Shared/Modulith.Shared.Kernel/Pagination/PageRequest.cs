namespace Modulith.Shared.Kernel.Pagination;

/// <summary>
/// A validated pagination request. Holds the canonical bounds for page number and page size
/// so every paginated handler references a single source of truth.
/// </summary>
public sealed record PageRequest
{
    public const int MaxPage = 10_000;
    public const int MaxPageSize = 100;

    public int Page { get; }
    public int PageSize { get; }

    /// <summary>Rows to skip. Safe for int arithmetic given the validated bounds.</summary>
    public int Offset => (Page - 1) * PageSize;

    private PageRequest(int page, int pageSize)
    {
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>
    /// Creates a PageRequest from already-validated values.
    /// Call only after checking bounds against <see cref="MaxPage"/> and <see cref="MaxPageSize"/>.
    /// </summary>
    public static PageRequest Of(int page, int pageSize) => new(page, pageSize);
}

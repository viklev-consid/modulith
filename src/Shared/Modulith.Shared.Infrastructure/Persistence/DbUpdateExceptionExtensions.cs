using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Modulith.Shared.Infrastructure.Persistence;

public static class DbUpdateExceptionExtensions
{
    // PostgreSQL error code for unique_violation
    private const string UniqueViolationSqlState = "23505";

    public static bool IsUniqueConstraintViolation(this DbUpdateException ex)
        => ex.InnerException is PostgresException pg &&
           string.Equals(pg.SqlState, UniqueViolationSqlState, StringComparison.Ordinal);
}

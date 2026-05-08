using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Modulith.Shared.Infrastructure.Persistence;

public static class DbUpdateExceptionExtensions
{
    // PostgreSQL error code for unique_violation
    private const string uniqueViolationSqlState = "23505";

    public static bool IsUniqueConstraintViolation(this DbUpdateException ex)
        => ex.InnerException is PostgresException pg &&
           string.Equals(pg.SqlState, uniqueViolationSqlState, StringComparison.Ordinal);

    public static bool IsUniqueConstraintViolation(this DbUpdateException ex, string constraintName)
        => ex.IsUniqueConstraintViolation() &&
           ex.InnerException is PostgresException { ConstraintName: not null } pg &&
           string.Equals(pg.ConstraintName, constraintName, StringComparison.Ordinal);
}

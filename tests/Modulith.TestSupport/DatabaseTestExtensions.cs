using Microsoft.EntityFrameworkCore;

namespace Modulith.TestSupport;

public static class DatabaseTestExtensions
{
    public static Task<TResult> QueryDbAsync<TDbContext, TResult>(
        this ApiTestFixture fixture,
        Func<TDbContext, Task<TResult>> query)
        where TDbContext : DbContext =>
        fixture.QueryDbAsync<TDbContext, TResult>((db, _) => query(db));

    public static Task ExecuteDbAsync<TDbContext>(
        this ApiTestFixture fixture,
        Func<TDbContext, Task> action)
        where TDbContext : DbContext =>
        fixture.ExecuteDbAsync<TDbContext>((db, _) => action(db));

    public static Task SeedDbAsync<TDbContext>(
        this ApiTestFixture fixture,
        Func<TDbContext, Task> seed)
        where TDbContext : DbContext =>
        fixture.SeedDbAsync<TDbContext>((db, _) => seed(db));
}

using Microsoft.EntityFrameworkCore;
using TickerQ.EntityFrameworkCore.Configurations;
using TickerQ.EntityFrameworkCore.DbContextFactory;
using TickerQ.Utilities.Entities;

namespace Modulith.Api.Infrastructure.Scheduling;

public sealed class TickerQOperationalDbContext(
    DbContextOptions<TickerQOperationalDbContext> options)
    : TickerQDbContext<TimeTickerEntity, CronTickerEntity>(options)
{
    public const string Schema = "tickerq";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TimeTickerConfigurations<TimeTickerEntity>(Schema));
        modelBuilder.ApplyConfiguration(new CronTickerConfigurations<CronTickerEntity>(Schema));
        modelBuilder.ApplyConfiguration(new CronTickerOccurrenceConfigurations<CronTickerEntity>(Schema));
    }
}

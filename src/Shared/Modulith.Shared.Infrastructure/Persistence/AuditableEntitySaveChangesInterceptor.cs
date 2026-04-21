using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Shared.Infrastructure.Persistence;

public sealed class AuditableEntitySaveChangesInterceptor(
    ICurrentUser currentUser,
    IClock clock)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditFields(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = clock.UtcNow;
        var actor = currentUser.Id;

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(nameof(IAuditableEntity.CreatedAt)).CurrentValue = now;
                    entry.Property(nameof(IAuditableEntity.CreatedBy)).CurrentValue = actor;
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(IAuditableEntity.UpdatedAt)).CurrentValue = now;
                    entry.Property(nameof(IAuditableEntity.UpdatedBy)).CurrentValue = actor;
                    break;
            }
        }
    }
}

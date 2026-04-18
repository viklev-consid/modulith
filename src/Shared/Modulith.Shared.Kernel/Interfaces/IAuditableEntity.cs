namespace Modulith.Shared.Kernel.Interfaces;

public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; }
    string? CreatedBy { get; }
    DateTimeOffset? UpdatedAt { get; }
    string? UpdatedBy { get; }
}

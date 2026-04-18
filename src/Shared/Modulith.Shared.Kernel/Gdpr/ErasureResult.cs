namespace Modulith.Shared.Kernel.Gdpr;

public sealed record ErasureResult(
    Guid UserId,
    ErasureStrategy StrategyApplied,
    int RecordsAffected,
    string? Notes = null);

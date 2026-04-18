namespace Modulith.Shared.Kernel.Interfaces;

public interface ICurrentUser
{
    string? Id { get; }
    string? Name { get; }
    bool IsAuthenticated { get; }
}

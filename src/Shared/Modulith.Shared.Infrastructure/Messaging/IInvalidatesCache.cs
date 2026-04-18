namespace Modulith.Shared.Infrastructure.Messaging;

public interface IInvalidatesCache
{
    string[] CacheKeys { get; }
}

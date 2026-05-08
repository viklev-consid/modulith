using WireMock.Server;

namespace Modulith.TestSupport;

public sealed class WireMockFixture : IAsyncDisposable, IDisposable
{
    private readonly WireMockServer server = WireMockServer.Start();

    public WireMockServer Server => server;

    public string Url => server.Url ?? throw new InvalidOperationException("WireMock server URL is not available.");

    public Uri Uri => new(Url);

    public void Reset() => server.Reset();

    public ValueTask DisposeAsync()
    {
        server.Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose() => server.Dispose();
}

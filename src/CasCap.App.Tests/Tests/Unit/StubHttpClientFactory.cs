namespace CasCap.Tests.Unit;

/// <summary>
/// <see cref="IHttpClientFactory"/> substitute handing out clients that are never used, satisfying
/// the health-check constructor without opening a socket.
/// </summary>
public sealed class StubHttpClientFactory : IHttpClientFactory
{
    /// <inheritdoc/>
    public HttpClient CreateClient(string name) => new() { Timeout = TimeSpan.FromSeconds(1) };
}

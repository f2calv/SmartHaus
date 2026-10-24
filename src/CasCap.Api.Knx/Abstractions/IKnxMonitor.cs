namespace CasCap.Abstractions;

/// <summary>Owns KNX connections and callbacks for a single leadership run.</summary>
public interface IKnxMonitor : IAsyncDisposable
{
    /// <summary>Discovers and connects buses using the ownership token.</summary>
    public Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Monitors connections and publishes telegrams until ownership ends.</summary>
    public Task RunAsync(CancellationToken cancellationToken);
}

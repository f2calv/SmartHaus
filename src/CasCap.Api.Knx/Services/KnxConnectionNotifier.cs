namespace CasCap.Services;

/// <summary>
/// Broadcasts KNX bus connection state changes via a <see cref="Channel{T}"/>.
/// </summary>
/// <remarks>
/// Registered as a singleton. <see cref="KnxMonitorBgService"/> calls
/// <see cref="Notify"/> internally; external consumers read from
/// <see cref="IKnxConnectionNotifier.Reader"/>.
/// </remarks>
public sealed class KnxConnectionNotifier : IKnxConnectionNotifier
{
    private readonly Channel<KnxConnectionStateChange> _channel =
        Channel.CreateUnbounded<KnxConnectionStateChange>(new UnboundedChannelOptions { SingleReader = true });

    /// <inheritdoc/>
    public ChannelReader<KnxConnectionStateChange> Reader => _channel.Reader;

    /// <summary>
    /// Publishes a connection state change to the channel.
    /// </summary>
    internal void Notify(KnxConnectionStateChange change) => _channel.Writer.TryWrite(change);
}

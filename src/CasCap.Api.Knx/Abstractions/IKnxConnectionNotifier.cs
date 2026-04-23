namespace CasCap.Abstractions;

/// <summary>
/// Provides a stream of KNX bus connection state changes for downstream consumers.
/// </summary>
/// <remarks>
/// <see cref="KnxMonitorBgService"/> publishes state changes to this notifier
/// when a bus connection drops or is re-established.
/// </remarks>
public interface IKnxConnectionNotifier
{
    /// <summary>
    /// A <see cref="ChannelReader{T}"/> that yields <see cref="KnxConnectionStateChange"/>
    /// events as they occur.
    /// </summary>
    ChannelReader<KnxConnectionStateChange> Reader { get; }
}

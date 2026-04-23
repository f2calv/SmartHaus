namespace CasCap.Services;

/// <summary>
/// In-process <see cref="IKnxTelegramBroker{T}"/> implementation backed by a bounded
/// <see cref="Channel{T}"/>. Suitable for single-pod deployments where all KNX services
/// run in the same process.
/// </summary>
/// <typeparam name="T">The telegram type being transported.</typeparam>
public class ChannelKnxTelegramBroker<T> : IKnxTelegramBroker<T>
{
    private readonly Channel<T> _channel = Channel.CreateBounded<T>(
        new BoundedChannelOptions(1_000) { SingleReader = true });

    /// <inheritdoc/>
    public ValueTask PublishAsync(T item, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(item, cancellationToken);

    /// <inheritdoc/>
    public IAsyncEnumerable<T> SubscribeAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

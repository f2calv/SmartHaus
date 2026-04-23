namespace CasCap.Abstractions;

/// <summary>
/// Abstracts the transport layer for KNX telegrams, decoupling producers and consumers
/// from the underlying messaging infrastructure. Implementations may use in-process
/// <see cref="System.Threading.Channels.Channel{T}"/> for single-pod deployments or
/// Redis streams for cross-pod communication in Kubernetes.
/// </summary>
/// <typeparam name="T">The telegram type being transported.</typeparam>
public interface IKnxTelegramBroker<T>
{
    /// <summary>
    /// Publishes a telegram to the broker for consumption by subscribers.
    /// </summary>
    /// <param name="item">The telegram to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PublishAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to the broker and yields telegrams as they arrive.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of telegrams.</returns>
    IAsyncEnumerable<T> SubscribeAsync(CancellationToken cancellationToken = default);
}

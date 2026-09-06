namespace CasCap.Abstractions;

/// <summary>
/// Transport-agnostic stream of inbound Signal messages.
/// </summary>
/// <remarks>
/// <para>
/// Both transports implement this interface with identical semantics, so a consumer can switch
/// between HTTP polling and the JSON-RPC WebSocket by changing
/// <see cref="CasCap.Models.SignalCliConfig.TransportMode"/> alone:
/// </para>
/// <list type="bullet">
///   <item>
///     <see cref="CasCap.Services.SignalCliRestClientService"/> polls
///     <c>GET /v1/receive/{number}</c> every
///     <see cref="CasCap.Models.SignalCliConfig.ReceivePollIntervalMs"/> milliseconds.
///   </item>
///   <item>
///     <see cref="CasCap.Services.SignalCliJsonRpcClientService"/> yields messages pushed over the
///     WebSocket as they arrive.
///   </item>
/// </list>
/// </remarks>
public interface ISignalCliReceiver
{
    /// <summary>
    /// Establishes the underlying receive transport. Optional — <see cref="StreamMessagesAsync"/>
    /// connects on demand — but calling it explicitly surfaces connection failures at startup
    /// rather than on the first message.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams inbound messages until the token is cancelled.
    /// </summary>
    /// <remarks>
    /// The stream is cold: enumeration starts the receive transport, and abandoning the enumerator
    /// stops it. Only one active enumeration per instance is expected; multiple concurrent consumers
    /// compete for messages rather than each receiving a copy.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token that ends the stream.</param>
    IAsyncEnumerable<SignalReceivedMessage> StreamMessagesAsync(CancellationToken cancellationToken = default);
}

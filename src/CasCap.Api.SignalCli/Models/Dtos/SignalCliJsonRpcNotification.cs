namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a JSON-RPC 2.0 notification pushed by the signal-cli REST API over the WebSocket
/// receive endpoint when the server is running in <c>json-rpc</c> or <c>json-rpc-native</c> mode.
/// </summary>
/// <remarks>
/// In JSON-RPC mode the WebSocket delivers messages in the following format instead of the
/// plain-object format returned by the REST polling endpoint:
/// <code>
/// {
///   "jsonrpc": "2.0",
///   "method": "receive",
///   "params": { "envelope": { ... }, "account": "+49..." }
/// }
/// </code>
/// The <see cref="Params"/> property contains the same <see cref="SignalReceivedMessage"/> payload
/// that the REST <c>GET /v1/receive/{number}</c> endpoint returns as array elements.
/// See <see href="https://github.com/bbernhard/signal-cli-rest-api/discussions/160"/> for details.
/// </remarks>
public record SignalCliJsonRpcNotification
{
    /// <summary>
    /// The JSON-RPC protocol version. Always <c>"2.0"</c> for spec-compliant servers.
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string? JsonRpc { get; init; }

    /// <summary>
    /// The JSON-RPC method name. Always <c>"receive"</c> for incoming message notifications.
    /// </summary>
    [JsonPropertyName("method")]
    public string? Method { get; init; }

    /// <summary>
    /// The notification payload, equivalent to a single element from the REST polling array.
    /// </summary>
    [JsonPropertyName("params")]
    public SignalReceivedMessage? Params { get; init; }
}

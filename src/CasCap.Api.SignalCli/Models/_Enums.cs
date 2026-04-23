namespace CasCap.Models;

/// <summary>
/// The transport mode used to communicate with the signal-cli REST API.
/// The <see cref="DescriptionAttribute"/> value should match the <c>MODE</c> environment variable
/// of the SignalCliRestServer instance.
/// </summary>
public enum SignalCliTransport
{
    /// <summary>
    /// Standard HTTP REST API with polling-based message reception via <c>GET /v1/receive/{number}</c>.
    /// Slowest performance, normal memory usage.
    /// </summary>
    [Description("normal")]
    Normal,

    /// <summary>
    /// Native REST API with polling-based message reception.
    /// Medium performance, normal memory usage.
    /// </summary>
    [Description("native")]
    Native,

    /// <summary>
    /// JSON-RPC mode with WebSocket-based push message reception via <c>ws://host/v1/receive/{number}</c>.
    /// Faster performance, increased memory usage.
    /// </summary>
    [Description("json-rpc")]
    JsonRpc,

    /// <summary>
    /// Native JSON-RPC mode with WebSocket-based push message reception.
    /// Fastest performance, normal memory usage.
    /// </summary>
    [Description("json-rpc-native")]
    JsonRpcNative,
}

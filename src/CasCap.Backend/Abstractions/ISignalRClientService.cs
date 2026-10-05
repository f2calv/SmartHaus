using Microsoft.AspNetCore.SignalR.Client;

namespace CasCap.Abstractions;

/// <summary>
/// Defines a managed SignalR hub connection that can connect to and disconnect from a hub URL.
/// </summary>
public interface ISignalRClientService
{
    /// <summary>
    /// Whether the client is currently connected to the hub.
    /// </summary>
    bool IsConnected { get; set; }

    /// <summary>
    /// The underlying <see cref="HubConnection"/>, available after <see cref="Connect"/> is called.
    /// </summary>
    HubConnection? connection { get; set; }

    /// <summary>
    /// Raised when a text message is received from the hub.
    /// </summary>
    event EventHandler<MessageEventArgs> MessageEvent;

    /// <summary>
    /// Establishes a connection to the hub at the specified URL, retrying indefinitely on failure.
    /// </summary>
    /// <param name="url">The full URL of the SignalR hub endpoint.</param>
    Task Connect(string url);

    /// <summary>
    /// Stops and disposes the active hub connection.
    /// </summary>
    Task Disconnect();
}

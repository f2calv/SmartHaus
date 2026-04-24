using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace CasCap.Services;

/// <summary>
/// Signal-cli client that uses a persistent WebSocket connection to <c>ws://host/v1/receive/{number}</c>
/// for push-based message reception, while delegating all other operations to the underlying
/// <see cref="SignalCliRestClientService"/> REST client.
/// </summary>
/// <remarks>
/// <para>
/// Requires the signal-cli-rest-api server to be running in <c>json-rpc</c> or <c>json-rpc-native</c> mode.
/// In JSON-RPC mode the only endpoint that changes behaviour is <c>/v1/receive/{number}</c> — it switches
/// from HTTP polling to a WebSocket push stream. All other REST endpoints (send, groups, attachments,
/// profiles, identities, contacts, devices, sticker packs, reactions, receipts, typing indicators, etc.)
/// continue to work as standard HTTP calls. Registration, verification, and device-linking endpoints are
/// <b>not available</b> in JSON-RPC mode per upstream documentation.
/// </para>
/// <para>
/// See <see href="https://bbernhard.github.io/signal-cli-rest-api/"/> for the full API specification and
/// <see href="https://github.com/bbernhard/signal-cli-rest-api/discussions/160"/> for JSON-RPC details.
/// </para>
/// </remarks>
public sealed class SignalCliJsonRpcClientService : INotifier, IDisposable, IAsyncDisposable
{
    private readonly ILogger<SignalCliJsonRpcClientService> _logger;
    private readonly SignalCliConfig _config;
    private readonly SignalCliRestClientService _restClient;
    private readonly Action<ClientWebSocket>? _configureWebSocket;

    private readonly ConcurrentQueue<SignalReceivedMessage> _messageBuffer = new();
    private readonly SemaphoreSlim _messageSignal = new(0);
    private readonly CancellationTokenSource _wsCts = new();

    private ClientWebSocket? _webSocket;
    private Task? _receiveLoopTask;
    private volatile bool _disposed;

    private readonly int _maxReconnectAttempts;
    private readonly TimeSpan _initialReconnectDelay;
    private readonly TimeSpan _maxReconnectDelay;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalCliJsonRpcClientService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="options">Signal-cli configuration.</param>
    /// <param name="restClient">The underlying REST client used for non-WebSocket operations.</param>
    /// <param name="configureWebSocket">
    /// Optional callback invoked on each <see cref="ClientWebSocket"/> before it connects.
    /// Use this to set authentication headers or other WebSocket options when the signal-cli
    /// REST API sits behind an authenticating reverse proxy (e.g. nginx basic auth).
    /// </param>
    public SignalCliJsonRpcClientService(ILogger<SignalCliJsonRpcClientService> logger, IOptions<SignalCliConfig> options,
        SignalCliRestClientService restClient, Action<ClientWebSocket>? configureWebSocket = null)
    {
        _logger = logger;
        _config = options.Value;
        _restClient = restClient;
        _configureWebSocket = configureWebSocket;

        _maxReconnectAttempts = _config.MaxReconnectAttempts;
        _initialReconnectDelay = TimeSpan.FromMilliseconds(_config.InitialReconnectDelayMs);
        _maxReconnectDelay = TimeSpan.FromMilliseconds(_config.MaxReconnectDelayMs);

        _logger.LogInformation("{ClassName} initialized, transport={Transport}, baseAddress={BaseAddress}",
            nameof(SignalCliJsonRpcClientService), _config.TransportMode, _config.BaseAddress);
    }

    /// <summary>
    /// Opens a WebSocket connection to the signal-cli REST API and starts the background
    /// receive loop that buffers incoming messages. Retries with exponential backoff when
    /// the server is not yet ready for WebSocket connections (e.g. returns HTTP 200 instead
    /// of the expected 101 Switching Protocols).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket is not null && _webSocket.State is WebSocketState.Open)
            return;

        // Dispose any previous WebSocket before creating a new one.
        _webSocket?.Dispose();
        _webSocket = null;

        var attempt = 0;
        while (true)
        {
            try
            {
                _webSocket = await CreateAndConnectWebSocketAsync(cancellationToken);

                _logger.LogInformation("{ClassName} WebSocket connected for {PhoneNumber}",
                    nameof(SignalCliJsonRpcClientService), _config.PhoneNumber.MaskPhoneNumber());

                _receiveLoopTask = Task.Run(() => ReceiveLoopWithReconnectAsync(_wsCts.Token), _wsCts.Token);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                attempt++;
                if (attempt > _maxReconnectAttempts)
                {
                    _logger.LogError(ex, "{ClassName} exceeded {MaxAttempts} initial connection attempts for {PhoneNumber}",
                        nameof(SignalCliJsonRpcClientService), _maxReconnectAttempts, _config.PhoneNumber.MaskPhoneNumber());
                    throw;
                }

                var delay = TimeSpan.FromTicks(Math.Min(
                    _initialReconnectDelay.Ticks * (1L << Math.Min(attempt - 1, 10)),
                    _maxReconnectDelay.Ticks));

                _logger.LogWarning(ex, "{ClassName} initial connection attempt {Attempt}/{MaxAttempts} failed, retrying in {Delay}",
                    nameof(SignalCliJsonRpcClientService), attempt, _maxReconnectAttempts, delay);

                _webSocket?.Dispose();
                _webSocket = null;

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    #region INotifier

    /// <inheritdoc/>
    async Task<INotificationResponse?> INotifier.SendAsync(INotificationMessage message, CancellationToken cancellationToken) =>
        await ((INotifier)_restClient).SendAsync(message, cancellationToken);

    /// <inheritdoc/>
    async Task<IReceivedNotification[]?> INotifier.ReceiveAsync(string account, CancellationToken cancellationToken)
    {
        if (_webSocket is null || _webSocket.State is not WebSocketState.Open)
        {
            _logger.LogDebug("{ClassName} WebSocket not open (state={State}), reconnecting before receive",
                nameof(SignalCliJsonRpcClientService), _webSocket?.State.ToString() ?? "null");
            await ConnectAsync(cancellationToken);
        }

        // Wait for at least one message to be buffered by the WebSocket receive loop,
        // providing natural back-pressure so the caller does not need a polling delay.
        await _messageSignal.WaitAsync(cancellationToken);

        var messages = DrainBuffer();

        // Drain excess semaphore counts so they stay in sync with the buffer.
        // Multiple messages may have arrived between the WaitAsync return and DrainBuffer.
        while (_messageSignal.CurrentCount > 0 && _messageSignal.Wait(0)) { }

        if (messages.Length > 0)
            _logger.LogDebug("{ClassName} drained {Count} buffered message(s) for {Account}",
                nameof(SignalCliJsonRpcClientService), messages.Length, account);
        return messages;
    }

    /// <inheritdoc/>
    Task<byte[]?> INotifier.GetAttachmentAsync(string attachmentId, CancellationToken cancellationToken) =>
        ((INotifier)_restClient).GetAttachmentAsync(attachmentId, cancellationToken);

    /// <inheritdoc/>
    Task<INotificationGroup[]?> INotifier.ListGroupsAsync(string account, CancellationToken cancellationToken) =>
        ((INotifier)_restClient).ListGroupsAsync(account, cancellationToken);

    /// <inheritdoc/>
    Task<bool> INotifier.StartProcessingAsync(string account, string recipient, CancellationToken cancellationToken) =>
        ((INotifier)_restClient).StartProcessingAsync(account, recipient, cancellationToken);

    /// <inheritdoc/>
    Task<bool> INotifier.StopProcessingAsync(string account, string recipient, CancellationToken cancellationToken) =>
        ((INotifier)_restClient).StopProcessingAsync(account, recipient, cancellationToken);

    /// <inheritdoc/>
    Task<bool> INotifier.SendProgressUpdateAsync(string account, string recipient, string reaction, string targetAuthor, long timestamp, CancellationToken cancellationToken) =>
        ((INotifier)_restClient).SendProgressUpdateAsync(account, recipient, reaction, targetAuthor, timestamp, cancellationToken);

    /// <inheritdoc/>
    Task<bool> INotifier.UpdateProfileNameAsync(string account, string displayName, CancellationToken cancellationToken) =>
        ((INotifier)_restClient).UpdateProfileNameAsync(account, displayName, cancellationToken);

    #endregion

    #region Private helpers

    /// <summary>
    /// Creates a new <see cref="ClientWebSocket"/> and connects to the signal-cli WebSocket endpoint.
    /// </summary>
    private async Task<ClientWebSocket> CreateAndConnectWebSocketAsync(CancellationToken cancellationToken)
    {
        var wsUri = BuildWebSocketUri(_config.BaseAddress, _config.PhoneNumber);
        _logger.LogInformation("{ClassName} connecting to WebSocket at {Uri}", nameof(SignalCliJsonRpcClientService),
            BuildWebSocketUri(_config.BaseAddress, _config.PhoneNumber.MaskPhoneNumber()));

        var ws = new ClientWebSocket();
        _configureWebSocket?.Invoke(ws);
        await ws.ConnectAsync(wsUri, cancellationToken);
        return ws;
    }

    private SignalReceivedMessage[] DrainBuffer()
    {
        var list = new List<SignalReceivedMessage>();
        while (_messageBuffer.TryDequeue(out var msg))
            list.Add(msg);
        return [.. list];
    }

    /// <summary>
    /// Wraps <see cref="ReceiveLoopAsync"/> with automatic reconnection using exponential backoff.
    /// When the inner loop exits (server close, network error) the WebSocket is re-established
    /// and the loop restarts, up to <see cref="MaxReconnectAttempts"/> consecutive failures.
    /// </summary>
    private async Task ReceiveLoopWithReconnectAsync(CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ReceiveLoopAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{ClassName} receive loop exited unexpectedly", nameof(SignalCliJsonRpcClientService));
            }

            if (cancellationToken.IsCancellationRequested)
                break;

            attempt++;
            if (attempt > _maxReconnectAttempts)
            {
                _logger.LogError("{ClassName} exceeded {MaxAttempts} reconnection attempts, giving up",
                    nameof(SignalCliJsonRpcClientService), _maxReconnectAttempts);
                break;
            }

            var delay = TimeSpan.FromTicks(Math.Min(
                _initialReconnectDelay.Ticks * (1L << Math.Min(attempt - 1, 10)),
                _maxReconnectDelay.Ticks));

            _logger.LogWarning("{ClassName} WebSocket disconnected, reconnecting in {Delay} (attempt {Attempt}/{MaxAttempts})",
                nameof(SignalCliJsonRpcClientService), delay, attempt, _maxReconnectAttempts);

            await Task.Delay(delay, cancellationToken);

            try
            {
                _webSocket?.Dispose();
                _webSocket = null;

                _webSocket = await CreateAndConnectWebSocketAsync(cancellationToken);

                // Reset attempt counter on successful reconnection.
                attempt = 0;
                _logger.LogInformation("{ClassName} WebSocket reconnected successfully",
                    nameof(SignalCliJsonRpcClientService));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{ClassName} reconnection attempt {Attempt} failed",
                    nameof(SignalCliJsonRpcClientService), attempt);
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();

        _logger.LogDebug("{ClassName} receive loop started, WebSocket state={State}",
            nameof(SignalCliJsonRpcClientService), _webSocket?.State);

        while (!cancellationToken.IsCancellationRequested && _webSocket?.State is WebSocketState.Open)
        {
            try
            {
                stream.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await _webSocket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType is WebSocketMessageType.Close)
                    {
                        _logger.LogWarning("{ClassName} WebSocket closed by server ({Status}: {Description})",
                            nameof(SignalCliJsonRpcClientService), result.CloseStatus, result.CloseStatusDescription);
                        return;
                    }
                    stream.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                var message = DeserializeMessage(stream);
                if (message is not null)
                {
                    _messageBuffer.Enqueue(message);
                    _messageSignal.Release();
                    _logger.LogDebug("{ClassName} buffered message from {Sender}, bufferDepth={BufferDepth}",
                        nameof(SignalCliJsonRpcClientService),
                        message.Envelope.Source ?? message.Envelope.SourceNumber ?? "unknown",
                        _messageBuffer.Count);
                }
                else
                {
                    stream.Position = 0;
                    var rawText = System.Text.Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
                    _logger.LogDebug("{ClassName} received WebSocket frame that could not be deserialized: {RawFrame}",
                        nameof(SignalCliJsonRpcClientService), rawText);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, "{ClassName} WebSocket receive error, state={State}",
                    nameof(SignalCliJsonRpcClientService), _webSocket?.State);
                break;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "{ClassName} failed to deserialize WebSocket message",
                    nameof(SignalCliJsonRpcClientService));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{ClassName} unexpected error in receive loop",
                    nameof(SignalCliJsonRpcClientService));
            }
        }

        _logger.LogDebug("{ClassName} receive loop exiting, WebSocket state={State}",
            nameof(SignalCliJsonRpcClientService), _webSocket?.State);
    }

    /// <summary>
    /// Attempts to deserialize a single WebSocket frame into a <see cref="SignalReceivedMessage"/>.
    /// The signal-cli REST API sends different formats depending on server mode:
    /// <list type="bullet">
    ///   <item><c>json-rpc</c> — raw payload: <c>{"envelope":{…},"account":"+49…"}</c></item>
    ///   <item><c>json-rpc-native</c> — JSON-RPC 2.0 notification: <c>{"jsonrpc":"2.0","method":"receive","params":{…}}</c></item>
    /// </list>
    /// This method tries the JSON-RPC wrapper first; if <see cref="SignalCliJsonRpcNotification.Params"/>
    /// is <see langword="null"/> it falls back to deserializing the frame as a raw
    /// <see cref="SignalReceivedMessage"/>.
    /// </summary>
    private static SignalReceivedMessage? DeserializeMessage(MemoryStream stream)
    {
        stream.Position = 0;

        // Try JSON-RPC 2.0 envelope first (json-rpc-native mode).
        try
        {
            var notification = JsonSerializer.Deserialize<SignalCliJsonRpcNotification>(stream);
            if (notification?.Params is not null)
                return notification.Params;
        }
        catch (JsonException) { /* not a JSON-RPC envelope, try raw format */ }

        // Fallback: raw payload without envelope (json-rpc mode).
        stream.Position = 0;
        try
        {
            return JsonSerializer.Deserialize<SignalReceivedMessage>(stream);
        }
        catch (JsonException) { /* unable to deserialize as either format */ return null; }
    }

    internal static Uri BuildWebSocketUri(string baseAddress, string phoneNumber)
    {
        var builder = new UriBuilder(baseAddress)
        {
            Scheme = baseAddress.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws",
            Path = $"v1/receive/{Uri.EscapeDataString(phoneNumber)}"
        };
        return builder.Uri;
    }

    #endregion

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _wsCts.Cancel();
        _webSocket?.Dispose();
        _wsCts.Dispose();
        _messageSignal.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        await _wsCts.CancelAsync();

        if (_webSocket is { State: WebSocketState.Open or WebSocketState.CloseReceived })
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "{ClassName} error during WebSocket close", nameof(SignalCliJsonRpcClientService));
            }
        }

        if (_receiveLoopTask is not null)
        {
            try { await _receiveLoopTask; }
            catch (OperationCanceledException) { /* expected */ }
        }

        _webSocket?.Dispose();
        _wsCts.Dispose();
        _messageSignal.Dispose();
    }
}

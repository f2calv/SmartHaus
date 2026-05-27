using Microsoft.AspNetCore.SignalR.Client;

namespace CasCap.Services;

/// <summary>
/// Abstract base class for SignalR event sinks that forward events to the consolidated HausHub
/// via the <c>/hubs/haus</c> endpoint. Handles hub connection lifecycle including eager
/// initialization, automatic reconnection and back-off retry.
/// </summary>
/// <typeparam name="T">The event type handled by this sink.</typeparam>
public abstract partial class HausSignalRSinkBase<T> : IEventSink<T> where T : class
{
    private readonly ILogger _logger;
    private readonly SignalRHubConfig _signalRHubConfig;
    private readonly ApiAuthConfig _apiAuthConfig;
    private HubConnection? _connection;
    private CancellationToken _cancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="HausSignalRSinkBase{T}"/> class.
    /// </summary>
    protected HausSignalRSinkBase(ILogger logger,
        IOptions<SignalRHubConfig> signalRHubConfig,
        IOptions<ApiAuthConfig> apiAuthConfig)
    {
        _logger = logger;
        _signalRHubConfig = signalRHubConfig.Value;
        _apiAuthConfig = apiAuthConfig.Value;
    }

    /// <inheritdoc/>
    public abstract string SinkType { get; }

    /// <summary>
    /// The name of the hub method to invoke when forwarding an event (e.g. <c>SendBuderusEvent</c>).
    /// </summary>
    protected abstract string HubMethodName { get; }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        var uri = new Uri(_signalRHubConfig.SignalRHub, _signalRHubConfig.HubPath);
        _logger.LogInformation("{ClassName} connecting to {Uri}", GetType().Name, uri);
        _connection = new HubConnectionBuilder()
            .WithUrl(uri, options =>
            {
                options.Headers.Add("Authorization", NetExtensions.GetBasicAuthHeaderValue(_apiAuthConfig.Username, _apiAuthConfig.Password));
            })
            .WithAutomaticReconnect()
            .Build();
        _connection.Reconnecting += OnReconnecting;
        _connection.Reconnected += OnReconnected;
        _connection.Closed += OnConnectionClosed;
        await ConnectWithRetryAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task WriteEvent(T data, CancellationToken cancellationToken = default)
    {
        LogWriteEvent(_logger, GetType().Name);
        if (_connection?.State != HubConnectionState.Connected)
        {
            LogHubNotConnected(_logger, GetType().Name);
            return;
        }
        try
        {
            await _connection.SendAsync(HubMethodName, data, cancellationToken);
        }
        catch (Exception ex)
        {
            LogSendError(_logger, ex, GetType().Name);
        }
    }


    #region private helpers

    private Task OnReconnecting(Exception? ex)
    {
        _logger.LogWarning(ex, "{ClassName} hub reconnecting...", GetType().Name);
        return Task.CompletedTask;
    }

    private Task OnReconnected(string? connectionId)
    {
        _logger.LogInformation("{ClassName} hub reconnected with {ConnectionId}", GetType().Name, connectionId);
        return Task.CompletedTask;
    }

    private async Task OnConnectionClosed(Exception? ex)
    {
        if (_cancellationToken.IsCancellationRequested)
            return;
        _logger.LogWarning(ex, "{ClassName} hub connection closed, restarting reconnect loop", GetType().Name);
        try
        {
            await ConnectWithRetryAsync(_cancellationToken);
        }
        catch (OperationCanceledException) { }
    }

    private async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        const int retrySeconds = 5;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _connection!.StartAsync(cancellationToken);
                _logger.LogInformation("{ClassName} connected to SignalR hub at {HubPath}", GetType().Name, _signalRHubConfig.HubPath);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ClassName} failed to connect to SignalR hub, retrying in {RetrySeconds}s",
                    GetType().Name, retrySeconds);
            }
            await Task.Delay(retrySeconds * 1_000, cancellationToken);
        }
    }

    #endregion

    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} sending event to SignalR hub")]
    private static partial void LogWriteEvent(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{ClassName} hub not connected, skipping event")]
    private static partial void LogHubNotConnected(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Error, Message = "{ClassName} error sending event via SignalR")]
    private static partial void LogSendError(ILogger logger, Exception ex, string className);
}

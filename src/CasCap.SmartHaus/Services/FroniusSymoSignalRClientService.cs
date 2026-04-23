using Microsoft.AspNetCore.SignalR.Client;

namespace CasCap.Services;

/// <summary>
/// Manages a SignalR client connection to the Fronius Symo hub, with automatic reconnect and retry logic.
/// </summary>
public class FroniusSymoSignalRClientService(ILogger<FroniusSymoSignalRClientService> logger, AppConfig appConfig) : ISignalRClientService
{
    /// <inheritdoc/>
    public bool IsConnected { get; set; } = false;

    /// <inheritdoc/>
    public HubConnection? connection { get; set; }

    /// <inheritdoc/>
    public event EventHandler<MessageEventArgs>? MessageEvent;

    /// <summary>Raises the <see cref="MessageEvent"/>.</summary>
    protected virtual void OnRaiseMessageEvent(MessageEventArgs args) { MessageEvent?.Invoke(this, args); }

    /// <inheritdoc/>
    public async Task Connect(string url)
    {
        url = url ?? throw new ArgumentNullException(nameof(url), "is required!");
        connection = new HubConnectionBuilder()
            .AddJsonProtocol(options =>
            {
                //options.PayloadSerializerSettings.ContractResolver = new DefaultContractResolver();
            })
            .WithUrl(url/*, HttpTransportType.WebSockets | HttpTransportType.LongPolling*/)
            .WithAutomaticReconnect()
            .Build();

        connection.On<string, string, DateTime>(nameof(IHausClientHub.ReceiveMessage), (user, message, date) =>
        {
            OnRaiseMessageEvent(new MessageEventArgs(user, message, date));
        });
        connection.Closed += async (error) =>
        {
            Debug.Assert(connection.State == HubConnectionState.Disconnected);
            await Task.Delay(new Random().Next(0, 5) * 1000);
            IsConnected = await ConnectWithRetryAsync();
        };
        connection.Reconnecting += error =>
        {
            Debug.Assert(connection.State == HubConnectionState.Reconnecting);
            return Task.CompletedTask;
        };
        connection.Reconnected += connectionId =>
        {
            Debug.Assert(connection.State == HubConnectionState.Connected);
            return Task.CompletedTask;
        };

        IsConnected = await ConnectWithRetryAsync();
        if (IsConnected)
        {
            var msg = $"{appConfig.PodName ?? AppDomain.CurrentDomain.FriendlyName} now connected to hub @ {url}";
            await connection.SendAsync(nameof(IHausServerHub.SendMessage), appConfig.PodName ?? AppDomain.CurrentDomain.FriendlyName, $"{msg}", DateTime.UtcNow);
        }

        async Task<bool> ConnectWithRetryAsync(CancellationToken cancellationToken = default)
        {
            var retry = 5;
            while (true)
            {
                try
                {
                    logger.LogInformation("{ClassName} attempting to connect to {Url}", nameof(FroniusSymoSignalRClientService), url);
                    await connection.StartAsync(cancellationToken);
                    Debug.Assert(connection.State == HubConnectionState.Connected);
                    return true;
                }
                catch (System.Net.Http.HttpRequestException hre)
                {
                    logger.LogError("{ClassName} {ExceptionMessage} Will retry in {Retry} seconds.", nameof(FroniusSymoSignalRClientService), hre.Message, retry);
                }
                catch when (cancellationToken.IsCancellationRequested)
                {
                    logger.LogError("{ClassName} cancellationToken.IsCancellationRequested", nameof(FroniusSymoSignalRClientService));
                    return false;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "{ClassName} {MethodName} failure", nameof(FroniusSymoSignalRClientService), nameof(ConnectWithRetryAsync));
                }
                await Task.Delay(retry * 1_000, cancellationToken);
            }
        }
    }

    /// <inheritdoc/>
    public async Task Disconnect()
    {
        if (connection?.State != HubConnectionState.Disconnected)
        {
            await connection!.StopAsync();
            await connection.DisposeAsync();
        }
    }
}

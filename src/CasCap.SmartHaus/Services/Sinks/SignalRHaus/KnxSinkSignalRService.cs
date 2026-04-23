namespace CasCap.Services;

/// <summary>
/// Event sink that forwards <see cref="KnxEvent"/> instances to the consolidated HausHub.
/// </summary>
[SinkType("SignalR")]
public class KnxSinkSignalRService(ILogger<KnxSinkSignalRService> logger,
    IOptions<SignalRHubConfig> signalRHubConfig,
    IOptions<ApiAuthConfig> apiAuthConfig)
    : HausSignalRSinkBase<KnxEvent>(logger, signalRHubConfig, apiAuthConfig)
{
    /// <inheritdoc/>
    protected override string HubMethodName => nameof(IHausServerHub.SendKnxTelegram);
}

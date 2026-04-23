namespace CasCap.Services;

/// <summary>
/// Event sink that forwards <see cref="FroniusEvent"/> instances to the consolidated HausHub.
/// </summary>
[SinkType("SignalR")]
public class FroniusSinkSignalRService(ILogger<FroniusSinkSignalRService> logger,
    IOptions<SignalRHubConfig> signalRHubConfig,
    IOptions<ApiAuthConfig> apiAuthConfig)
    : HausSignalRSinkBase<FroniusEvent>(logger, signalRHubConfig, apiAuthConfig)
{
    /// <inheritdoc/>
    protected override string HubMethodName => nameof(IHausServerHub.SendFroniusEvent);
}

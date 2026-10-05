namespace CasCap.Services;

/// <summary>
/// Event sink that forwards <see cref="FroniusEvent"/> instances to the consolidated HausHub.
/// </summary>
[SinkType("SignalR")]
public sealed class FroniusSinkSignalRService(ILogger<FroniusSinkSignalRService> logger,
    IOptions<SignalRHubConfig> signalRHubConfig,
    IOptions<ApiAuthConfig> apiAuthConfig)
    : HausSignalRSinkBase<FroniusEvent>(logger, signalRHubConfig, apiAuthConfig)
{
    /// <inheritdoc/>
    public override string SinkType => "SignalR";

    /// <inheritdoc/>
    protected override string HubMethodName => nameof(IHausServerHub.SendFroniusEvent);
}

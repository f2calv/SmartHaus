namespace CasCap.Services;

/// <summary>
/// Event sink that forwards <see cref="BuderusEvent"/> instances to the consolidated HausHub.
/// </summary>
[SinkType("SignalR")]
public sealed class BuderusSinkSignalRService(ILogger<BuderusSinkSignalRService> logger,
    IOptions<SignalRHubConfig> signalRHubConfig,
    IOptions<ApiAuthConfig> apiAuthConfig)
    : HausSignalRSinkBase<BuderusEvent>(logger, signalRHubConfig, apiAuthConfig)
{
    /// <inheritdoc/>
    public override string SinkType => "SignalR";

    /// <inheritdoc/>
    protected override string HubMethodName => nameof(IHausServerHub.SendBuderusEvent);
}

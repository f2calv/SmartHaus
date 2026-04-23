namespace CasCap.Services;

/// <summary>
/// Event sink that forwards <see cref="DoorBirdEvent"/> instances to the consolidated HausHub.
/// </summary>
[SinkType("SignalR")]
public class DoorBirdSinkSignalRService(ILogger<DoorBirdSinkSignalRService> logger,
    IOptions<SignalRHubConfig> signalRHubConfig,
    IOptions<ApiAuthConfig> apiAuthConfig)
    : HausSignalRSinkBase<DoorBirdEvent>(logger, signalRHubConfig, apiAuthConfig)
{
    /// <inheritdoc/>
    protected override string HubMethodName => nameof(IHausServerHub.SendDoorBirdEvent);
}

namespace CasCap.Services;

/// <summary>
/// Event sink that forwards <see cref="KnxEvent"/> instances to the consolidated HausHub.
/// </summary>
[SinkType("SignalR")]
public sealed class KnxSinkSignalRService(ILogger<KnxSinkSignalRService> logger,
    IOptions<SignalRHubConfig> signalRHubConfig,
    IOptions<ApiAuthConfig> apiAuthConfig)
    : HausSignalRSinkBase<KnxEvent>(logger, signalRHubConfig, apiAuthConfig)
{
    /// <inheritdoc/>
    public override string SinkType => "SignalR";

    /// <inheritdoc/>
    protected override string HubMethodName => nameof(IHausServerHub.SendKnxTelegram);
}

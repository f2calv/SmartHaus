namespace CasCap.Services;

/// <summary>Default <see cref="ICommsGroupRouter"/> that sends configured operational sources to the monitor group.</summary>
/// <remarks>
/// Events whose <see cref="CommsEvent.Source"/> is in <see cref="CommsAgentConfig.MonitorSources"/> go to
/// <see cref="CommsAgentConfig.MonitorGroupName"/> when one is configured; everything else goes to
/// <see cref="CommsAgentConfig.GroupName"/>.
/// </remarks>
public sealed class MonitorSourcesGroupRouter(IOptions<CommsAgentConfig> commsAgentConfig) : ICommsGroupRouter
{
    /// <inheritdoc/>
    public string ResolveGroup(CommsEvent commsEvent) =>
        commsAgentConfig.Value.MonitorGroupName is { Length: > 0 } monitorGroup
            && commsAgentConfig.Value.MonitorSources.Contains(commsEvent.Source)
            ? monitorGroup
            : commsAgentConfig.Value.GroupName;
}

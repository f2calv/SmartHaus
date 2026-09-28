namespace CasCap.Services;

/// <summary>Default <see cref="ICommsGroupRouter"/> that sends configured operational sources to the monitor group.</summary>
/// <remarks>
/// Events whose <see cref="CommsEvent.Source"/> is in <see cref="CommsConfig.MonitorSources"/> go to
/// <see cref="CommsConfig.MonitorGroupName"/> when one is configured; everything else goes to
/// <see cref="CommsConfig.GroupName"/>.
/// </remarks>
public sealed class MonitorSourcesGroupRouter(IOptions<CommsConfig> commsConfig) : ICommsGroupRouter
{
    /// <inheritdoc/>
    public string ResolveGroup(CommsEvent commsEvent) =>
        commsConfig.Value.MonitorGroupName is { Length: > 0 } monitorGroup
            && commsConfig.Value.MonitorSources.Contains(commsEvent.Source)
            ? monitorGroup
            : commsConfig.Value.GroupName;
}

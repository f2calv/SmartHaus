namespace CasCap.Models;

/// <summary>
/// Configuration for the heating agent that monitors Buderus DHW setpoint events
/// and posts alerts to the comms stream.
/// </summary>
/// <remarks>
/// These settings are consumed by <c>BuderusSinkCommsStreamService</c> — they are
/// application-level orchestration concerns rather than Buderus KM200 device settings.
/// Bound from the <c>Settings</c> sub-section of <see cref="AgentKeys.HeatingAgent"/>
/// in <c>AIConfig.Agents</c>.
/// </remarks>
public record HeatingAgentConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName =>
        $"{nameof(CasCap)}:{nameof(AgentKeys.AIConfig)}:{nameof(AgentKeys.Agents)}:{nameof(AgentKeys.HeatingAgent)}:{nameof(AgentKeys.Settings)}";

    /// <summary>
    /// Hysteresis in °C for the DHW1 setpoint alert.
    /// </summary>
    /// <remarks>
    /// The alert re-arms when the actual temperature drops this many degrees below the
    /// current setpoint. Used by <c>BuderusSinkCommsStreamService</c>. Defaults to <c>1.0</c>.
    /// </remarks>
    [Range(0.1, 10.0)]
    public double Dhw1AlertHysteresis { get; init; } = 1.0;

    /// <summary>
    /// Minimum cooldown in milliseconds between consecutive DHW1 setpoint alerts.
    /// </summary>
    /// <remarks>
    /// Used by <c>BuderusSinkCommsStreamService</c>. Defaults to <c>3600000</c> (1 hour).
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int Dhw1AlertCooldownMs { get; init; } = 3_600_000;
}

namespace CasCap.Models;

/// <summary>
/// Represents the measurable quantities reported by a Shelly smart plug.
/// Each value maps directly to the same-named property on <see cref="ShellyEvent"/>
/// and carries its OTel metric name, unit, and description via <see cref="MetricAttribute"/>.
/// </summary>
public enum ShellyFunction
{
    /// <summary>
    /// Instantaneous power consumption of the connected load in Watts.
    /// </summary>
    [Metric("smartplug.power", "W", Description = "Instantaneous power consumption in Watts")]
    Power,

    /// <summary>
    /// Whether the relay is currently on (1) or off (0).
    /// </summary>
    [Metric("smartplug.relay.state", "1", Description = "Relay state — 1 = on, 0 = off")]
    RelayState,

    /// <summary>
    /// Internal device temperature in degrees Celsius.
    /// </summary>
    [Metric("smartplug.temperature", "Cel", Description = "Internal device temperature in Celsius")]
    Temperature,

    /// <summary>
    /// Whether an overpower condition has been detected (1) or not (0).
    /// </summary>
    [Metric("smartplug.overpower", "1", Description = "Overpower condition — 1 = triggered, 0 = normal")]
    Overpower,
}

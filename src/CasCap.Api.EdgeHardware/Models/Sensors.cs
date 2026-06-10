namespace CasCap.Models;

/// <summary>
/// Sensor hardware configuration.
/// </summary>
public sealed record Sensors
{
    /// <summary>
    /// HC-SR501 PIR motion sensor configuration.
    /// </summary>
    public HcSr501 HcSr501 { get; init; } = new();
}

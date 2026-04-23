namespace CasCap.Models;

/// <summary>
/// HC-SR501 PIR motion sensor configuration.
/// </summary>
public record HcSr501
{
    /// <summary>
    /// GPIO output pin number for the sensor.
    /// </summary>
    public int OutPin { get; init; }
}

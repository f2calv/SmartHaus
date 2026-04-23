namespace CasCap.Models.Dtos;

/// <summary>
/// A unit/value pair returned by inverter real-time data endpoints.
/// </summary>
public record UnitValue
{
    /// <summary>
    /// The measurement unit (e.g. "W", "A", "V", "Hz", "Wh", "VA", "°C").
    /// </summary>
    [Description("The measurement unit (e.g. \"W\", \"A\", \"V\", \"Hz\", \"Wh\", \"VA\", \"°C\").")]
    public string? Unit { get; init; }

    /// <summary>
    /// The measured value.
    /// </summary>
    [Description("The measured value.")]
    public double? Value { get; init; }
}

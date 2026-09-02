namespace CasCap.Models.Dtos;

/// <summary>
/// A unit/value pair returned by inverter real-time data endpoints.
/// </summary>
public sealed record UnitValue
{
    /// <summary>
    /// The measurement unit (e.g. "W", "A", "V", "Hz", "Wh", "VA", "�C").
    /// </summary>
    [Description("The measurement unit (e.g. \"W\", \"A\", \"V\", \"Hz\", \"Wh\", \"VA\", \"�C\").")]
    public string? Unit { get; init; }

    /// <summary>
    /// The measured value.
    /// </summary>
    [Description("The measured value.")]
    public double? Value { get; init; }

    /// <summary>
    /// Measured values keyed by inverter device ID.
    /// </summary>
    /// <remarks>Newer firmware returns this property for system-scoped inverter data; older firmware may return <see cref="Value"/>.</remarks>
    [Description("Measured values keyed by inverter device ID.")]
    public Dictionary<string, double?>? Values { get; init; }
}

namespace CasCap.Models.Dtos;

/// <summary>
/// A temperature value with unit returned by the Miele API.
/// </summary>
public record TemperatureValue
{
    /// <summary>
    /// The raw temperature value (degrees Celsius × 100). -32768 indicates unused.
    /// </summary>
    [Description("The raw temperature value (degrees Celsius × 100). -32768 indicates unused.")]
    public int? value_raw { get; init; }

    /// <summary>
    /// The localized temperature value.
    /// </summary>
    [Description("The localized temperature value.")]
    public object? value_localized { get; init; }

    /// <summary>
    /// The measurement unit (e.g. "Celsius").
    /// </summary>
    [Description("The measurement unit (e.g. \"Celsius\").")]
    public string? unit { get; init; }
}

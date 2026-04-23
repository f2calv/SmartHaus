namespace CasCap.Models.Dtos;

/// <summary>
/// Meter device details (manufacturer, model, serial).
/// </summary>
public record MeterDetails
{
    /// <summary>
    /// Meter manufacturer name.
    /// </summary>
    [Description("Meter manufacturer name.")]
    public string? Manufacturer { get; init; }

    /// <summary>
    /// Meter model identifier.
    /// </summary>
    [Description("Meter model identifier.")]
    public string? Model { get; init; }

    /// <summary>
    /// Meter serial number.
    /// </summary>
    [Description("Meter serial number.")]
    public string? Serial { get; init; }
}

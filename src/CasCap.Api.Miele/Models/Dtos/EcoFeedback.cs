namespace CasCap.Models.Dtos;

/// <summary>
/// Eco feedback data showing water and energy consumption.
/// </summary>
public record EcoFeedback
{
    /// <summary>
    /// Current water consumption.
    /// </summary>
    [Description("Current water consumption.")]
    public ConsumptionValue? currentWaterConsumption { get; init; }

    /// <summary>
    /// Current energy consumption.
    /// </summary>
    [Description("Current energy consumption.")]
    public ConsumptionValue? currentEnergyConsumption { get; init; }

    /// <summary>
    /// Relative water usage forecast for the selected program (0–1).
    /// </summary>
    [Description("Relative water usage forecast for the selected program (0–1).")]
    public double? waterForecast { get; init; }

    /// <summary>
    /// Relative energy usage forecast for the selected program (0–1).
    /// </summary>
    [Description("Relative energy usage forecast for the selected program (0–1).")]
    public double? energyForecast { get; init; }
}

/// <summary>
/// A consumption measurement with unit.
/// </summary>
public record ConsumptionValue
{
    /// <summary>
    /// The measurement unit (e.g. "l", "kWh").
    /// </summary>
    [Description("The measurement unit (e.g. \"l\", \"kWh\").")]
    public string? unit { get; init; }

    /// <summary>
    /// The consumption value.
    /// </summary>
    [Description("The consumption value.")]
    public double? value { get; init; }
}

namespace CasCap.Models.Dtos;

/// <summary>
/// Storage real-time data entry from <c>GetStorageRealtimeData.cgi</c>.
/// </summary>
public record StorageRealtimeData
{
    /// <summary>
    /// Storage controller data.
    /// </summary>
    [Description("Storage controller data.")]
    public StorageController? Controller { get; init; }

    /// <summary>
    /// Storage module entries (may be empty).
    /// </summary>
    [Description("Storage module entries (may be empty).")]
    public object[]? Modules { get; init; }
}

/// <summary>
/// Storage controller data containing battery state and capacity information.
/// </summary>
public record StorageController
{
    /// <summary>
    /// Controller device details.
    /// </summary>
    [Description("Controller device details.")]
    public MeterDetails? Details { get; init; }

    /// <summary>
    /// Maximum capacity in watt-hours.
    /// </summary>
    [Description("Maximum capacity in watt-hours.")]
    public double Capacity_Maximum { get; init; }

    /// <summary>
    /// DC current in amps.
    /// </summary>
    [Description("DC current in amps.")]
    public double Current_DC { get; init; }

    /// <summary>
    /// Designed capacity in watt-hours.
    /// </summary>
    [Description("Designed capacity in watt-hours.")]
    public double DesignedCapacity { get; init; }

    /// <summary>
    /// Whether the storage is enabled (1 = enabled).
    /// </summary>
    [Description("Whether the storage is enabled (1 = enabled).")]
    public double Enable { get; init; }

    /// <summary>
    /// Relative state of charge as a percentage (0–100).
    /// </summary>
    [Description("Relative state of charge as a percentage (0–100).")]
    public double StateOfCharge_Relative { get; init; }

    /// <summary>
    /// Battery cell status code.
    /// </summary>
    [Description("Battery cell status code.")]
    public double Status_BatteryCell { get; init; }

    /// <summary>
    /// Battery cell temperature in degrees Celsius.
    /// </summary>
    [Description("Battery cell temperature in degrees Celsius.")]
    public double Temperature_Cell { get; init; }

    /// <summary>
    /// Unix timestamp of the measurement.
    /// </summary>
    [Description("Unix timestamp of the measurement.")]
    public long TimeStamp { get; init; }

    /// <summary>
    /// DC voltage in volts.
    /// </summary>
    [Description("DC voltage in volts.")]
    public double Voltage_DC { get; init; }
}

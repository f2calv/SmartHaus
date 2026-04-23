namespace CasCap.Models.Dtos;

/// <summary>
/// Inverter device status returned inside common inverter data.
/// </summary>
public record DeviceStatus
{
    /// <summary>
    /// The device error code (0 = no error).
    /// </summary>
    [Description("The device error code (0 = no error).")]
    public int ErrorCode { get; init; }

    /// <summary>
    /// The inverter state string (e.g. "Running", "Sleeping").
    /// </summary>
    [Description("The inverter state string (e.g. \"Running\", \"Sleeping\").")]
    public string? InverterState { get; init; }

    /// <summary>
    /// The device status code.
    /// </summary>
    [Description("The device status code.")]
    public int StatusCode { get; init; }
}

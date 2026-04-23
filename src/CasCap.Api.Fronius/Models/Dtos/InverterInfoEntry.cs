namespace CasCap.Models.Dtos;

/// <summary>
/// Inverter info entry for a single device from <c>GetInverterInfo.cgi</c>.
/// </summary>
public record InverterInfoEntry
{
    /// <summary>
    /// User-defined custom name for the inverter.
    /// </summary>
    [Description("User-defined custom name for the inverter.")]
    public string? CustomName { get; init; }

    /// <summary>
    /// Device type identifier.
    /// </summary>
    [Description("Device type identifier.")]
    public int DT { get; init; }

    /// <summary>
    /// Current error code (0 = no error).
    /// </summary>
    [Description("Current error code (0 = no error).")]
    public int ErrorCode { get; init; }

    /// <summary>
    /// The inverter state string (e.g. "Running").
    /// </summary>
    [Description("The inverter state string (e.g. \"Running\").")]
    public string? InverterState { get; init; }

    /// <summary>
    /// Maximum PV power rating in watts.
    /// </summary>
    [Description("Maximum PV power rating in watts.")]
    public int PVPower { get; init; }

    /// <summary>
    /// Whether this inverter is visible in the UI (1 = visible).
    /// </summary>
    [Description("Whether this inverter is visible in the UI (1 = visible).")]
    public int Show { get; init; }

    /// <summary>
    /// Current status code.
    /// </summary>
    [Description("Current status code.")]
    public int StatusCode { get; init; }

    /// <summary>
    /// Unique device identifier (serial number).
    /// </summary>
    [Description("Unique device identifier (serial number).")]
    public string? UniqueID { get; init; }
}

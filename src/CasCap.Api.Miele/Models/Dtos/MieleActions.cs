namespace CasCap.Models.Dtos;

/// <summary>
/// Available actions for a Miele device from <c>GET /devices/{deviceId}/actions</c>.
/// </summary>
public record MieleActions
{
    /// <summary>
    /// Available process actions (1=Start, 2=Stop, 3=Pause, 4–7=Superfreezing/Supercooling).
    /// </summary>
    [Description("Available process actions (1=Start, 2=Stop, 3=Pause, 4–7=Superfreezing/Supercooling).")]
    public int[]? processAction { get; init; }

    /// <summary>
    /// Available light actions (1=Enable, 2=Disable).
    /// </summary>
    [Description("Available light actions (1=Enable, 2=Disable).")]
    public int[]? light { get; init; }

    /// <summary>
    /// Available ambient light actions (1=Enable, 2=Disable).
    /// </summary>
    [Description("Available ambient light actions (1=Enable, 2=Disable).")]
    public int[]? ambientLight { get; init; }

    /// <summary>
    /// Available start time settings.
    /// </summary>
    [Description("Available start time settings.")]
    public int[][]? startTime { get; init; }

    /// <summary>
    /// Available ventilation steps.
    /// </summary>
    [Description("Available ventilation steps.")]
    public int[]? ventilationStep { get; init; }

    /// <summary>
    /// Available program IDs.
    /// </summary>
    [Description("Available program IDs.")]
    public int[]? programId { get; init; }

    /// <summary>
    /// Available target temperature zones with min/max.
    /// </summary>
    [Description("Available target temperature zones with min/max.")]
    public object[]? targetTemperature { get; init; }

    /// <summary>
    /// Whether the device name can be changed.
    /// </summary>
    [Description("Whether the device name can be changed.")]
    public bool? deviceName { get; init; }

    /// <summary>
    /// Whether the device can be powered on.
    /// </summary>
    [Description("Whether the device can be powered on.")]
    public bool? powerOn { get; init; }

    /// <summary>
    /// Whether the device can be powered off.
    /// </summary>
    [Description("Whether the device can be powered off.")]
    public bool? powerOff { get; init; }

    /// <summary>
    /// Available ambient light colour presets.
    /// </summary>
    [Description("Available ambient light colour presets.")]
    public string[]? colors { get; init; }

    /// <summary>
    /// Available operating modes (0=Normal, 1=Sabbath, 2=Party, 3=Holiday).
    /// </summary>
    [Description("Available operating modes (0=Normal, 1=Sabbath, 2=Party, 3=Holiday).")]
    public int[]? modes { get; init; }

    /// <summary>
    /// Available run-on times in minutes.
    /// </summary>
    [Description("Available run-on times in minutes.")]
    public int[]? runOnTime { get; init; }
}

namespace CasCap.Models.Dtos;

/// <summary>
/// Current operational state of a Miele device.
/// </summary>
public record MieleState
{
    /// <summary>
    /// The current program.
    /// </summary>
    [Description("The current program.")]
    public LocalizedValue? ProgramID { get; init; }

    /// <summary>
    /// The main device status (1=Off, 2=On, 5=Running, 8=Failure, etc.).
    /// </summary>
    [Description("The main device status (1=Off, 2=On, 5=Running, 8=Failure, etc.).")]
    public LocalizedValue? status { get; init; }

    /// <summary>
    /// The current program type (0=Normal, 1=Own, 2=Automatic, 3=Cleaning/Care).
    /// </summary>
    [Description("The current program type (0=Normal, 1=Own, 2=Automatic, 3=Cleaning/Care).")]
    public LocalizedValue? programType { get; init; }

    /// <summary>
    /// The current program phase.
    /// </summary>
    [Description("The current program phase.")]
    public LocalizedValue? programPhase { get; init; }

    /// <summary>
    /// Remaining time as [hours, minutes].
    /// </summary>
    [Description("Remaining time as [hours, minutes].")]
    public int[]? remainingTime { get; init; }

    /// <summary>
    /// Relative start time as [hours, minutes].
    /// </summary>
    [Description("Relative start time as [hours, minutes].")]
    public int[]? startTime { get; init; }

    /// <summary>
    /// Target temperatures (1–3 zones).
    /// </summary>
    [Description("Target temperatures (1–3 zones).")]
    public TemperatureValue[]? targetTemperature { get; init; }

    /// <summary>
    /// Current temperatures (1–3 zones).
    /// </summary>
    [Description("Current temperatures (1–3 zones).")]
    public TemperatureValue[]? temperature { get; init; }

    /// <summary>
    /// Whether an info notification is active.
    /// </summary>
    [Description("Whether an info notification is active.")]
    public bool? signalInfo { get; init; }

    /// <summary>
    /// Whether a failure notification is active.
    /// </summary>
    [Description("Whether a failure notification is active.")]
    public bool? signalFailure { get; init; }

    /// <summary>
    /// Whether a door-open message is active.
    /// </summary>
    [Description("Whether a door-open message is active.")]
    public bool? signalDoor { get; init; }

    /// <summary>
    /// Remote control status.
    /// </summary>
    [Description("Remote control status.")]
    public RemoteEnable? remoteEnable { get; init; }

    /// <summary>
    /// Light state (1=On, 2=Off).
    /// </summary>
    [Description("Light state (1=On, 2=Off).")]
    public int? light { get; init; }

    /// <summary>
    /// Elapsed time since program start as [hours, minutes].
    /// </summary>
    [Description("Elapsed time since program start as [hours, minutes].")]
    public int[]? elapsedTime { get; init; }

    /// <summary>
    /// Spinning speed information.
    /// </summary>
    [Description("Spinning speed information.")]
    public LocalizedValue? spinningSpeed { get; init; }

    /// <summary>
    /// Drying step information.
    /// </summary>
    [Description("Drying step information.")]
    public LocalizedValue? dryingStep { get; init; }

    /// <summary>
    /// Ventilation step information.
    /// </summary>
    [Description("Ventilation step information.")]
    public LocalizedValue? ventilationStep { get; init; }

    /// <summary>
    /// Cooking zone plate steps.
    /// </summary>
    [Description("Cooking zone plate steps.")]
    public LocalizedValue[]? plateStep { get; init; }

    /// <summary>
    /// Eco feedback data (water/energy consumption).
    /// </summary>
    [Description("Eco feedback data (water/energy consumption).")]
    public EcoFeedback? ecoFeedback { get; init; }

    /// <summary>
    /// Battery level as a percentage (0–100), or null if not applicable.
    /// </summary>
    [Description("Battery level as a percentage (0–100), or null if not applicable.")]
    public int? batteryLevel { get; init; }
}

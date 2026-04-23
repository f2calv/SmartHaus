namespace CasCap.Models.Dtos;

/// <summary>
/// A device program from <c>GET /devices/{deviceId}/programs</c>.
/// </summary>
public record MieleProgram
{
    /// <summary>
    /// The appliance-specific program ID.
    /// </summary>
    [Description("The appliance-specific program ID.")]
    public int programId { get; init; }

    /// <summary>
    /// The localized program name.
    /// </summary>
    [Description("The localized program name.")]
    public string? program { get; init; }

    /// <summary>
    /// Optional program parameters (temperature, duration).
    /// </summary>
    [Description("Optional program parameters (temperature, duration).")]
    public MieleProgramParameters? parameters { get; init; }
}

/// <summary>
/// Optional parameters for a Miele program.
/// </summary>
public record MieleProgramParameters
{
    /// <summary>
    /// Temperature settings for the program.
    /// </summary>
    [Description("Temperature settings for the program.")]
    public MieleProgramRange? temperature { get; init; }

    /// <summary>
    /// Duration settings for the program.
    /// </summary>
    [Description("Duration settings for the program.")]
    public MieleProgramDuration? duration { get; init; }
}

/// <summary>
/// Temperature range parameters for a program.
/// </summary>
public record MieleProgramRange
{
    /// <summary>
    /// The lowest selectable temperature.
    /// </summary>
    [Description("The lowest selectable temperature.")]
    public int min { get; init; }

    /// <summary>
    /// The highest selectable temperature.
    /// </summary>
    [Description("The highest selectable temperature.")]
    public int max { get; init; }

    /// <summary>
    /// The step width for temperature selection.
    /// </summary>
    [Description("The step width for temperature selection.")]
    public int step { get; init; }

    /// <summary>
    /// Whether this parameter is mandatory.
    /// </summary>
    [Description("Whether this parameter is mandatory.")]
    public bool mandatory { get; init; }
}

/// <summary>
/// Duration range parameters for a program.
/// </summary>
public record MieleProgramDuration
{
    /// <summary>
    /// The minimum duration as [hours, minutes].
    /// </summary>
    [Description("The minimum duration as [hours, minutes].")]
    public int[]? min { get; init; }

    /// <summary>
    /// The maximum duration as [hours, minutes].
    /// </summary>
    [Description("The maximum duration as [hours, minutes].")]
    public int[]? max { get; init; }

    /// <summary>
    /// Whether this parameter is mandatory.
    /// </summary>
    [Description("Whether this parameter is mandatory.")]
    public bool mandatory { get; init; }
}

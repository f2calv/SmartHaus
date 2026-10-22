using System.Text.Json.Serialization;

namespace CasCap.Models.Dtos;

/// <summary>A partial appliance action supported by the Miele API.</summary>
/// <remarks>Supported actions depend on the appliance and its current state.</remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MieleActionRequest : IValidatableObject
{
    // TODO: Add runOnTime only after Miele resolves its array schema versus scalar example:
    // https://developer.miele.com/docs/third-party-api/swagger.json.
    // Both shapes were considered; reject the field rather than guess a device command.

    /// <summary>Process action: start, stop, pause, or a cooling/freezing action (1-7).</summary>
    /// <example>1</example>
    [Description("Process action: 1 start, 2 stop, 3 pause, 4 start superfreezing, 5 stop superfreezing, 6 start supercooling, 7 stop supercooling.")]
    [Range(1, 7)]
    public int? ProcessAction { get; init; }

    /// <summary>Enables (1) or disables (2) the light.</summary>
    /// <example>1</example>
    [Description("Light action: 1 enable, 2 disable.")]
    [Range(1, 2)]
    public int? Light { get; init; }

    /// <summary>Enables (1) or disables (2) ambient lighting.</summary>
    /// <example>2</example>
    [Description("Ambient light action: 1 enable, 2 disable.")]
    [Range(1, 2)]
    public int? AmbientLight { get; init; }

    /// <summary>Ambient colour name or hexadecimal RGB value.</summary>
    /// <example>#8B0000</example>
    [Description("Ambient colour name or hexadecimal RGB string.")]
    public string? Colors { get; init; }

    /// <summary>Personal appliance display name.</summary>
    /// <example>Example appliance</example>
    [Description("Personal appliance display name.")]
    public string? DeviceName { get; init; }

    /// <summary>Operation mode: normal (0), Sabbath (1), party (2), holiday (3).</summary>
    /// <example>0</example>
    [Description("Operation mode: 0 normal, 1 Sabbath, 2 party, 3 holiday.")]
    [Range(0, 3)]
    public int? Modes { get; init; }

    /// <summary>Requests appliance power-on.</summary>
    /// <example>true</example>
    [Description("Requests appliance power-on.")]
    public bool? PowerOn { get; init; }

    /// <summary>Requests appliance power-off.</summary>
    /// <example>true</example>
    [Description("Requests appliance power-off.")]
    public bool? PowerOff { get; init; }

    /// <summary>Appliance-specific program identifier.</summary>
    /// <example>4</example>
    [Description("Appliance-specific program identifier.")]
    public int? ProgramId { get; init; }

    /// <summary>Relative start delay as hours and minutes.</summary>
    /// <example>[2,45]</example>
    [Description("Relative start delay as exactly two integers: hours and minutes.")]
    [MinLength(2), MaxLength(2)]
    public int[]? StartTime { get; init; }

    /// <summary>Target temperature values for explicitly selected zones.</summary>
    /// <example>[{"zone":2,"value":-20}]</example>
    [Description("Target temperatures by zone; values depend on the appliance.")]
    [MinLength(1)]
    public MieleTargetTemperatureRequest[]? TargetTemperature { get; init; }

    /// <summary>Ventilation level (1-4).</summary>
    /// <example>3</example>
    [Description("Ventilation level from 1 to 4; availability depends on the appliance.")]
    [Range(1, 4)]
    public int? VentilationStep { get; init; }

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ProcessAction is null && Light is null && AmbientLight is null && Colors is null &&
            DeviceName is null && Modes is null && PowerOn is null && PowerOff is null &&
            ProgramId is null && StartTime is null && TargetTemperature is null && VentilationStep is null)
            yield return new ValidationResult("At least one supported appliance action is required.");
        if (StartTime is { Length: 2 } time && (time[0] < 0 || time[1] is < 0 or > 59))
            yield return new ValidationResult("Start time must contain non-negative hours and minutes from 0 to 59.", [nameof(StartTime)]);
        if (TargetTemperature is { } temperatures && temperatures.Any(temperature => temperature is null))
            yield return new ValidationResult("Every target temperature must specify a zone and value.", [nameof(TargetTemperature)]);
    }
}

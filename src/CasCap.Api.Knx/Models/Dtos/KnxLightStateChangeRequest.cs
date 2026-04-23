namespace CasCap.Models.Dtos;

/// <summary>
/// Request payload for setting the state of a KNX lighting group address.
/// Exactly one of <see cref="IsOn"/>, <see cref="DimValue"/> or <see cref="HexColour"/> must be set.
/// </summary>
public record KnxLightStateChangeRequest : IValidatableObject
{
    /// <inheritdoc cref="KnxGroupAddressGroup.GroupName"/>
    /// <example>DG-LI-Office-DL-South</example>
    public required string GroupName { get; init; }

    /// <summary>
    /// Switches the light on (<see langword="true"/>) or off (<see langword="false"/>).
    /// Mutually exclusive with <see cref="DimValue"/> and <see cref="HexColour"/>.
    /// </summary>
    /// <example>true</example>
    public bool? IsOn { get; init; }

    /// <summary>
    /// Absolute dimming value as a percentage — 0 is off, 100 is full brightness.
    /// Mutually exclusive with <see cref="IsOn"/> and <see cref="HexColour"/>.
    /// </summary>
    /// <example>50</example>
    [Range(0, 100)]
    public double? DimValue { get; init; }

    /// <summary>
    /// Six-character HEX string to set the RGB colour (e.g. <c>ff0000</c> for red,
    /// <c>00ff00</c> for green, <c>0000ff</c> for blue).
    /// Mutually exclusive with <see cref="IsOn"/> and <see cref="DimValue"/>.
    /// </summary>
    /// <example>ff0000</example>
    [Length(6, 6, ErrorMessage = "HEX values must be 6 characters long e.g. ff0000 for red, 00ff00 for green, 0000ff for blue.")]
    public string? HexColour { get; init; }

    /// <summary>
    /// Resolves the <see cref="LightingFunction"/> function/feedback pairs and values to send
    /// based on whichever property (<see cref="IsOn"/>, <see cref="DimValue"/> or <see cref="HexColour"/>) is set.
    /// </summary>
    /// <param name="isDimmed">
    /// When <see cref="IsOn"/> is <see langword="false"/>, indicates whether the light was
    /// previously dimmed (i.e. a <see cref="LightingFunction.VFB"/> address exists and its
    /// value is not <c>100</c>). Dimmable lights that have been dimmed require
    /// <see cref="LightingFunction.VAL"/>=0 to clear the dimming priority; non-dimmable
    /// lights (or dimmable lights still at 100%) use <see cref="LightingFunction.SW"/>=<see langword="false"/>.
    /// </param>
    /// <returns>
    /// A list containing the function <see cref="LightingFunction"/>, the feedback <see cref="LightingFunction"/>
    /// and the value to send as an <see cref="object"/>; or <see langword="null"/> if no property is set.
    /// </returns>
    /// <remarks>
    /// When <see cref="IsOn"/> is <see langword="false"/> and <paramref name="isDimmed"/> is
    /// <see langword="true"/>, the resolved function is
    /// <see cref="LightingFunction.VAL"/>=0 rather than <see cref="LightingFunction.SW"/>=<see langword="false"/>.
    /// MDT dimming actuators give absolute dimming values priority over switching commands,
    /// so an <see cref="LightingFunction.SW"/> Off is ignored when a non-zero <see cref="LightingFunction.VAL"/> is active.
    /// Sending <see cref="LightingFunction.VAL"/>=0 clears the
    /// dimming priority and the actuator automatically sets <see cref="LightingFunction.SW_FB"/>
    /// to <see langword="false"/>.
    /// Note: the downside of this is that any time we wish to turn off a dimmed light it will
    /// take longer than a normal 'switch off' scenario.
    /// When <paramref name="isDimmed"/> is <see langword="false"/> (the light is non-dimmable
    /// or has never been dimmed away from 100%), a simple
    /// <see cref="LightingFunction.SW"/>=<see langword="false"/> is sent instead, which is
    /// faster and works for lights that do not have a <see cref="LightingFunction.VAL"/>
    /// function configured in the KNX system.
    /// </remarks>
    public List<(object Function, object Feedback, object Value)>? Resolve(bool isDimmed = false) => this switch
    {
        { IsOn: false } when isDimmed => [(LightingFunction.VAL, (object)LightingFunction.VFB, (object)0d)],
        { IsOn: false } => [(LightingFunction.SW, (object)LightingFunction.SW_FB, (object)false)],
        { IsOn: true } => [(LightingFunction.SW, (object)LightingFunction.SW_FB, (object)true)],
        { DimValue: not null } => [(LightingFunction.VAL, (object)LightingFunction.VFB, (object)DimValue.Value)],
        { HexColour: not null } => [(LightingFunction.RGB, (object)LightingFunction.RGB_FB, (object)HexColour)],
        _ => null,
    };

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (KnxGroupAddressParsed.ParseCategory(GroupName) is not GroupAddressCategory.LI)
            yield return new ValidationResult(
                $"{nameof(GroupName)} must contain the {nameof(GroupAddressCategory.LI)} category segment.",
                [nameof(GroupName)]);

        var setCount = (IsOn is not null ? 1 : 0)
            + (DimValue is not null ? 1 : 0)
            + (HexColour is not null ? 1 : 0);

        if (setCount == 0)
            yield return new ValidationResult(
                $"Exactly one of {nameof(IsOn)}, {nameof(DimValue)} or {nameof(HexColour)} must be provided.",
                [nameof(IsOn), nameof(DimValue), nameof(HexColour)]);
        else if (setCount > 1)
            yield return new ValidationResult(
                $"Only one of {nameof(IsOn)}, {nameof(DimValue)} or {nameof(HexColour)} may be set at a time.",
                [nameof(IsOn), nameof(DimValue), nameof(HexColour)]);
    }
}

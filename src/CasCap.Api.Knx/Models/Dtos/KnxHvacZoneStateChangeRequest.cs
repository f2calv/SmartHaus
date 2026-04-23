namespace CasCap.Models.Dtos;

/// <summary>
/// Request payload for adjusting the temperature setpoint of a KNX HVAC group address.
/// Sends to <see cref="HvacFunction.SETP_UPDATE"/> and polls
/// <see cref="HvacFunction.SETP"/> for confirmation.
/// </summary>
public record KnxHvacZoneStateChangeRequest : IValidatableObject
{
    /// <inheritdoc cref="KnxGroupAddressGroup.GroupName"/>
    /// <example>DG-HZ-Office</example>
    public required string GroupName { get; init; }

    /// <summary>
    /// The desired temperature setpoint in degrees Celsius.
    /// The value is sent to <see cref="HvacFunction.SETP_UPDATE"/> and the command is
    /// skipped when the current <see cref="HvacFunction.SETP"/> already matches.
    /// </summary>
    /// <example>19</example>
    [Range(14, 25)]
    public required double SetpointAdjust { get; init; }

    /// <summary>
    /// Resolves the <see cref="HvacFunction"/> function/feedback pair and value to send.
    /// </summary>
    /// <returns>
    /// A list containing the <see cref="HvacFunction.SETP_UPDATE"/> function,
    /// the <see cref="HvacFunction.SETP"/> feedback and the <see cref="SetpointAdjust"/> value.
    /// </returns>
    public List<(object Function, object Feedback, object Value)> Resolve()
        => [(HvacFunction.SETP_UPDATE, (object)HvacFunction.SETP, (object)SetpointAdjust)];

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (KnxGroupAddressParsed.ParseCategory(GroupName) is not GroupAddressCategory.HZ)
            yield return new ValidationResult(
                $"{nameof(GroupName)} must contain the {nameof(GroupAddressCategory.HZ)} category segment.",
                [nameof(GroupName)]);
    }
}

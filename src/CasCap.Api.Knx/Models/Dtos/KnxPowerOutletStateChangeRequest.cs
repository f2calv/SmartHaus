namespace CasCap.Models.Dtos;

/// <summary>
/// Request payload for switching a KNX power outlet on or off.
/// </summary>
public record KnxPowerOutletStateChangeRequest : IValidatableObject
{
    /// <inheritdoc cref="KnxGroupAddressGroup.GroupName"/>
    /// <example>DG-SD-Office-South</example>
    public required string GroupName { get; init; }

    /// <summary>
    /// Switches the power outlet on (<see langword="true"/>) or off (<see langword="false"/>).
    /// </summary>
    /// <example>true</example>
    public required bool IsOn { get; init; }

    /// <summary>
    /// Resolves the <see cref="PowerOutletFunction"/> function/feedback pair and value to send.
    /// </summary>
    /// <returns>
    /// A list containing the <see cref="PowerOutletFunction.SD_SW"/> function,
    /// the <see cref="PowerOutletFunction.SD_FB"/> feedback and the <see cref="IsOn"/> value.
    /// </returns>
    public List<(object Function, object Feedback, object Value)> Resolve()
        => [(PowerOutletFunction.SD_SW, (object)PowerOutletFunction.SD_FB, (object)IsOn)];

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (KnxGroupAddressParsed.ParseCategory(GroupName) is not GroupAddressCategory.SD)
            yield return new ValidationResult(
                $"{nameof(GroupName)} must contain the {nameof(GroupAddressCategory.SD)} category segment.",
                [nameof(GroupName)]);
    }
}

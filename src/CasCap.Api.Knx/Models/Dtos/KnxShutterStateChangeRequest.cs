namespace CasCap.Models.Dtos;

/// <summary>
/// Request payload for setting the position and/or slats of a KNX blind or shutter group address.
/// At least one of <see cref="VPosition"/> or <see cref="Slats"/> must be set.
/// </summary>
public record KnxShutterStateChangeRequest : IValidatableObject
{
    /// <inheritdoc cref="KnxGroupAddressGroup.GroupName"/>
    /// <example>DG-BL-North-L</example>
    public required string GroupName { get; init; }

    /// <summary>
    /// The vertical position value as a percentage (0..100).
    /// Mutually independent of <see cref="Slats"/> — both may be set in a single request.
    /// 0 is fully open, 100 is fully closed.
    /// </summary>
    /// <example>90</example>
    [Range(0, 100)]
    public double? VPosition { get; init; }

    /// <summary>
    /// The slats position value as a percentage (0..100).
    /// Mutually independent of <see cref="VPosition"/> — both may be set in a single request.
    /// 0 is slats fully open, 100 is slats fully closed.
    /// </summary>
    /// <example>50</example>
    [Range(0, 100)]
    public double? Slats { get; init; }

    /// <summary>
    /// Resolves the <see cref="ShutterFunction"/> function/feedback pairs and values to send
    /// based on whichever properties (<see cref="VPosition"/> and/or <see cref="Slats"/>) are set.
    /// </summary>
    /// <param name="lastDirectionDown">
    /// The last known movement direction of the shutter, obtained from the
    /// <see cref="ShutterFunction.DIRECTION"/> feedback address. <see langword="true"/> means
    /// the shutter last moved down, <see langword="false"/> means it last moved up, and
    /// <see langword="null"/> means the direction is unknown (no state available).
    /// When <see langword="false"/> (last moved up) and <see cref="Slats"/> is set, the slats
    /// value is inverted (<c>100 - Slats</c>) because the <see cref="ShutterFunction.POSSLATS"/>
    /// command is always applied relative to the last direction of travel.
    /// </param>
    /// <returns>
    /// A list of tuples containing the function <see cref="ShutterFunction"/>, the feedback <see cref="ShutterFunction"/>
    /// and the value to send as an <see cref="object"/>; or <see langword="null"/> if no property is set.
    /// </returns>
    public List<(object Function, object Feedback, object Value)>? Resolve(bool? lastDirectionDown = null)
    {
        var results = new List<(object, object, object)>(2);

        if (VPosition is not null)
            results.Add((ShutterFunction.POS, ShutterFunction.POS_FB, VPosition.Value));

        if (Slats is not null)
        {
            var adjustedSlats = lastDirectionDown is false
                ? 100d - Slats.Value
                : Slats.Value;
            results.Add((ShutterFunction.POSSLATS, ShutterFunction.POSSLATS_FB, adjustedSlats));
        }

        return results.Count > 0 ? results : null;
    }

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (KnxGroupAddressParsed.ParseCategory(GroupName) is not GroupAddressCategory.BL)
            yield return new ValidationResult(
                $"{nameof(GroupName)} must contain the {nameof(GroupAddressCategory.BL)} category segment.",
                [nameof(GroupName)]);

        if (VPosition is null && Slats is null)
            yield return new ValidationResult(
                $"At least one of {nameof(VPosition)} or {nameof(Slats)} must be provided.",
                [nameof(VPosition), nameof(Slats)]);
    }
}

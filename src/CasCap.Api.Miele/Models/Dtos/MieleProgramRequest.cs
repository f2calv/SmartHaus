using System.Text.Json.Serialization;

namespace CasCap.Models.Dtos;

/// <summary>Selects an appliance program with optional duration and temperature.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MieleProgramRequest : IValidatableObject
{
    /// <summary>Appliance-specific program identifier.</summary>
    /// <example>24</example>
    [Description("Required appliance-specific program identifier.")]
    [Required]
    public int? ProgramId { get; init; }

    /// <summary>Optional program duration as hours and minutes.</summary>
    /// <example>[1,15]</example>
    [Description("Optional duration as exactly two integers: hours and minutes.")]
    [MinLength(2), MaxLength(2)]
    public int[]? Duration { get; init; }

    /// <summary>Optional program temperature, when supported by the appliance.</summary>
    /// <example>220</example>
    [Description("Optional program temperature; supported values depend on the appliance.")]
    public int? Temperature { get; init; }

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Duration is { Length: 2 } time && (time[0] < 0 || time[1] is < 0 or > 59))
            yield return new ValidationResult("Duration must contain non-negative hours and minutes from 0 to 59.", [nameof(Duration)]);
    }
}

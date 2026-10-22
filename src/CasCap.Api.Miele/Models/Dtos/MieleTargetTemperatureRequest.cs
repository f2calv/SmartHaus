using System.Text.Json.Serialization;

namespace CasCap.Models.Dtos;

/// <summary>A target temperature for a selected appliance zone.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MieleTargetTemperatureRequest
{
    /// <summary>Appliance-specific zone identifier.</summary>
    /// <example>2</example>
    [Description("Appliance-specific zone identifier.")]
    public required int Zone { get; init; }

    /// <summary>Target temperature; supported values depend on the appliance.</summary>
    /// <example>-20</example>
    [Description("Target temperature; supported values depend on the appliance.")]
    public required int Value { get; init; }
}

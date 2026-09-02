namespace CasCap.Models.Dtos;

/// <summary>
/// Smart-load devices returned by the power-flow endpoint.
/// </summary>
public sealed record Smartloads
{
    /// <summary>
    /// Ohmpilot Eco devices keyed by device ID.
    /// </summary>
    /// <remarks>The device payload is preserved because its shape depends on the connected model.</remarks>
    [Description("Ohmpilot Eco devices keyed by device ID.")]
    public Dictionary<string, JsonElement>? OhmpilotEcos { get; init; }

    /// <summary>
    /// Ohmpilot devices keyed by device ID.
    /// </summary>
    /// <remarks>The device payload is preserved because its shape depends on the connected model.</remarks>
    [Description("Ohmpilot devices keyed by device ID.")]
    public Dictionary<string, JsonElement>? Ohmpilots { get; init; }
}
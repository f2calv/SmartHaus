namespace CasCap.Models.Dtos;

/// <summary>
/// Combined ident and state for a Miele device from <c>GET /devices/{deviceId}</c>.
/// </summary>
public record MieleDevice
{
    /// <summary>
    /// Device identification information.
    /// </summary>
    [Description("Device identification information.")]
    public MieleIdent? ident { get; init; }

    /// <summary>
    /// Current device operational state.
    /// </summary>
    [Description("Current device operational state.")]
    public MieleState? state { get; init; }
}

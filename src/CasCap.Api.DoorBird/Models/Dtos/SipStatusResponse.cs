namespace CasCap.Models.Dtos;

/// <summary>
/// Response wrapper for the DoorBird SIP status endpoint (<c>sip.cgi</c>).
/// </summary>
public record SipStatusResponse
{
    /// <summary>
    /// The BHA response containing the return code and SIP configuration.
    /// </summary>
    [JsonPropertyName("BHA")]
    public required BHASipStatus Bha { get; init; }
}

namespace CasCap.Models.Dtos;

/// <summary>
/// Request payload for sending a value to the KNX bus.
/// </summary>
public record KnxStateChangeRequest
{
    /// <summary>
    /// The KNX group address name (e.g. EG-LI-Entrance-DL-SW).
    /// </summary>
    public required string GroupAddressName { get; init; }

    /// <summary>
    /// The value to send (e.g. True, False, 50).
    /// </summary>
    public required string ActualValue { get; init; }
}

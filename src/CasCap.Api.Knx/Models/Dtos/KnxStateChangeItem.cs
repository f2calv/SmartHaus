namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a single resolved state change operation queued for background processing.
/// Contains either <see cref="Resolved"/> entries (function/feedback pairs with polling)
/// or <see cref="DirectWrites"/> (fire-and-forget bus writes without feedback polling).
/// </summary>
public record KnxStateChangeItem
{
    /// <summary>
    /// The base group name (e.g. <c>DG-LI-Office-DL-South</c>).
    /// </summary>
    public required string GroupName { get; init; }

    /// <summary>
    /// The resolved function/feedback/value tuples to send to the KNX bus.
    /// Each entry is sent sequentially and polled for feedback confirmation.
    /// </summary>
    public List<(object Function, object Feedback, object Value)>? Resolved { get; init; }

    /// <summary>
    /// Direct group address writes that are sent to the KNX bus without feedback polling.
    /// Each entry is the full group address name and the value to send.
    /// Used by internal services such as <see cref="CasCap.Services.KnxAutomationBgService"/>.
    /// </summary>
    public List<(string GroupAddressName, object Value)>? DirectWrites { get; init; }
}

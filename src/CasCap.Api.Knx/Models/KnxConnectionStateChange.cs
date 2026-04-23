namespace CasCap.Models;

/// <summary>
/// Represents a KNX bus connection state transition (dropped or reconnected).
/// </summary>
public record KnxConnectionStateChange
{
    /// <summary>
    /// The area/line whose connection changed.
    /// </summary>
    public required KnxAreaLine AreaLine { get; init; }

    /// <summary>
    /// <see langword="true"/> when the connection was (re)established;
    /// <see langword="false"/> when it was lost.
    /// </summary>
    public required bool Connected { get; init; }

    /// <summary>
    /// UTC timestamp when the state change was observed.
    /// </summary>
    public required DateTime TimestampUtc { get; init; }
}

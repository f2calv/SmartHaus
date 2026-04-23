namespace CasCap.Models;

/// <summary>
/// Represents the current state of a KNX group address including its decoded value, value label and timestamp.
/// </summary>
public record State
{
    /// <summary>Initializes a new instance of the <see cref="State"/> record.</summary>
    public State() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="State"/> record.
    /// </summary>
    /// <param name="groupAddress">The KNX group address name.</param>
    /// <param name="value">The decoded value string.</param>
    /// <param name="valueLabel">The optional human-readable label for the decoded value (e.g. "Open", "Closed").</param>
    /// <param name="timestampUtc">The UTC timestamp of the telegram.</param>
    [SetsRequiredMembers]
    public State(string groupAddress, string value, string? valueLabel, DateTime timestampUtc)
    {
        GroupAddress = groupAddress;
        Value = value;
        ValueLabel = valueLabel;
        TimestampUtc = timestampUtc;
    }

    /// <summary>
    /// The KNX group address name.
    /// </summary>
    [Description("KNX group address name (e.g. EG-LI-Entrance-DL-SW_FB).")]
    public required string GroupAddress { get; init; }

    /// <summary>
    /// The decoded value as a string.
    /// </summary>
    [Description("Decoded value as a string (e.g. '21.5', 'True', '75').")]
    public required string Value { get; init; }

    /// <summary>
    /// The optional human-readable label for the decoded bus value (e.g. "Open", "Closed", "Window").
    /// </summary>
    /// <remarks>This will be null when the <see cref="Value"/> is a numeric value, e.g. for Temperature or Humidity.</remarks>
    [Description("Human-readable label for the decoded value (e.g. 'On', 'Off', 'Open', 'Closed'). Null for numeric values.")]
    public string? ValueLabel { get; init; }

    /// <summary>
    /// The UTC <see cref="DateTime"/> of the last state update.
    /// </summary>
    [Description("UTC timestamp of the last state update.")]
    public required DateTime TimestampUtc { get; init; }

    /// <summary>
    /// Determines equality based on <see cref="GroupAddress"/>, <see cref="Value"/> and <see cref="ValueLabel"/> only,
    /// excluding <see cref="TimestampUtc"/>.
    /// </summary>
    public virtual bool Equals(State? other)
        => other is not null
        && GroupAddress == other.GroupAddress
        && Value == other.Value
        && ValueLabel == other.ValueLabel;

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(GroupAddress, Value, ValueLabel);
}

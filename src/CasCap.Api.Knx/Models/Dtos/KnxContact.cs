namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a KNX binary-input contact (door sensor, window sensor, presence detector)
/// with decoded feedback state from <see cref="ContactFunction"/>.
/// </summary>
public sealed record KnxContact
{
    /// <inheritdoc cref="KnxGroupAddressGroup.GroupName"/>
    [Description("Group name (e.g. EG-BI-Entrance(FrontDoor)-East).")]
    public required string GroupName { get; init; }

    /// <inheritdoc cref="FloorType" path="/summary"/>
    [Description("Building floor. Values: KG (basement), EG (ground), OG (upper), DG (top).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FloorType? Floor { get; init; }

    /// <inheritdoc cref="RoomType" path="/summary"/>
    [Description("Room name (e.g. Kitchen, Office, LivingRoom).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RoomType? Room { get; init; }

    /// <inheritdoc cref="KnxGroupAddressGroup.Location"/>
    [Description("Free-text location detail extracted from parentheses in the group address name.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Location { get; init; }

    /// <inheritdoc cref="Models.CompassOrientation" path="/summary"/>
    [Description("Compass orientation. Values: North, East, South, West.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CompassOrientation? Orientation { get; init; }

    /// <inheritdoc cref="ContactFunction.STATE"/>
    [Description("Current contact state. Values: Inactive (closed/off/false), Active (open/on/true).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DptState? State { get; init; }

    /// <summary>
    /// Creates a <see cref="KnxContact"/> from a <see cref="KnxGroupAddressGroup"/> whose
    /// children have been enriched with state values.
    /// </summary>
    /// <param name="group">The contact group with bound state.</param>
    internal static KnxContact FromGroup(KnxGroupAddressGroup group)
        => new()
        {
            GroupName = group.GroupName,
            Floor = group.Floor,
            Room = group.Room,
            Location = group.Location,
            Orientation = group.Orientation,
            State = ToState(group),
        };

    #region Private Helpers

    private static DptState? ToState(KnxGroupAddressGroup group)
    {
        var child = group.Children.FirstOrDefault(c => c.Function == ContactFunction.STATE.ToString());
        if (child is null)
            return null;

        if (child.Value is "True" or "1")
            return DptState.Active;
        if (child.Value is "False" or "0")
            return DptState.Inactive;

        if (Enum.TryParse<DptState>(child.ValueLabel, true, out var state))
            return state;

        if (child.ValueLabel?.Equals("open", StringComparison.OrdinalIgnoreCase) == true)
            return DptState.Active;
        if (child.ValueLabel?.Equals("closed", StringComparison.OrdinalIgnoreCase) == true)
            return DptState.Inactive;

        return null;
    }

    #endregion
}

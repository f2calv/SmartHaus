namespace CasCap.Models.Dtos;

/// <summary>Represents a KNX Boolean door-lock sensor with its current locked or unlocked state.</summary>
public sealed record KnxDoorLock
{
    /// <inheritdoc cref="KnxGroupAddressGroup.GroupName"/>
    [Description("Group name (e.g. EG-BI-Entrance(Main DoorLock)-North).")]
    public required string GroupName { get; init; }

    /// <inheritdoc cref="FloorType" path="/summary"/>
    [Description("Building floor. Values: KG (basement), EG (ground), OG (upper), DG (top).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FloorType? Floor { get; init; }

    /// <inheritdoc cref="RoomType" path="/summary"/>
    [Description("Room name (e.g. Entrance or GuestRoom).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RoomType? Room { get; init; }

    /// <inheritdoc cref="KnxGroupAddressGroup.Location"/>
    [Description("Door-lock location extracted from parentheses in the group address name.")]
    public required string Location { get; init; }

    /// <inheritdoc cref="Models.CompassOrientation" path="/summary"/>
    [Description("Compass orientation. Values: North, East, South, West.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CompassOrientation? Orientation { get; init; }

    /// <inheritdoc cref="DptLockState" path="/summary"/>
    [Description("Current lock state. Values: Locked (false), Unlocked (true), or null when unknown.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DptLockState? State { get; init; }

    /// <summary>Creates a door-lock projection from a classified group with bound state.</summary>
    /// <param name="group">The door-lock group with bound state.</param>
    internal static KnxDoorLock FromGroup(KnxGroupAddressGroup group)
        => new()
        {
            GroupName = group.GroupName,
            Floor = group.Floor,
            Room = group.Room,
            Location = group.Location!,
            Orientation = group.Orientation,
            State = ToState(group),
        };

    #region Private Helpers

    private static DptLockState? ToState(KnxGroupAddressGroup group)
    {
        var child = group.Children.FirstOrDefault(c =>
            c.Function == ContactFunction.STATE.ToString()
            && c.DPTs == "DPST-1-2");

        return child?.Value switch
        {
            "False" or "0" => DptLockState.Locked,
            "True" or "1" => DptLockState.Unlocked,
            _ => null,
        };
    }

    #endregion
}
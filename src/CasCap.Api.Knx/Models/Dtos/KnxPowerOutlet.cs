namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a KNX power outlet with decoded feedback values for each <see cref="PowerOutletFunction"/>.
/// </summary>
public record KnxPowerOutlet
{
    /// <inheritdoc cref="KnxGroupAddressGroup.GroupName"/>
    [Description("Group name (e.g. DG-SD-Office).")]
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

    /// <inheritdoc cref="PowerOutletFunction.SD_FB"/>
    [Description("Current power outlet state. Values: Inactive (off), Active (on).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DptState? Switch { get; init; }

    /// <summary>
    /// Creates a <see cref="KnxPowerOutlet"/> from a <see cref="KnxGroupAddressGroup"/> whose
    /// children have been enriched with state values.
    /// </summary>
    /// <param name="group">The power outlet group with bound state.</param>
    internal static KnxPowerOutlet FromGroup(KnxGroupAddressGroup group)
        => new()
        {
            GroupName = group.GroupName,
            Floor = group.Floor,
            Room = group.Room,
            Location = group.Location,
            Switch = ToEnum<DptState>(group, PowerOutletFunction.SD_FB),
        };

    #region Private Helpers

    private static T? ToEnum<T>(KnxGroupAddressGroup group, PowerOutletFunction function) where T : struct, Enum
    {
        var child = group.Children.FirstOrDefault(c => c.Function == function.ToString());
        if (child?.ValueLabel is null)
            return null;

        return Enum.TryParse<T>(child.ValueLabel, true, out var result) ? result : null;
    }

    #endregion
}

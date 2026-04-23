namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a KNX shutter/blind with decoded feedback values for each <see cref="ShutterFunction"/>.
/// </summary>
public record KnxShutter
{
    /// <inheritdoc cref="KnxGroupAddressGroup.GroupName"/>
    [Description("Group name (e.g. OG-BL-FamilyBathroom-West).")]
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

    /// <inheritdoc cref="ShutterFunction.POS_FB"/>
    [Description("Current position percentage. IMPORTANT: 0=fully open, 100=fully closed (KNX convention).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Position { get; init; }

    /// <inheritdoc cref="ShutterFunction.POSSLATS_FB"/>
    [Description("Slats position percentage. IMPORTANT: 0=slats fully open, 100=slats fully closed (KNX convention).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? SlatsPosition { get; init; }

    /// <inheritdoc cref="ShutterFunction.DIRECTION"/>
    [Description("Last movement direction. Values: Up, Down.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DptUpDown? Direction { get; init; }

    /// <inheritdoc cref="ShutterFunction.DIAG"/>
    [Description("Diagnostics string from the shutter actuator.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Diagnostics { get; init; }

    /// <summary>
    /// Creates a <see cref="KnxShutter"/> from a <see cref="KnxGroupAddressGroup"/> whose
    /// children have been enriched with state values.
    /// </summary>
    /// <param name="group">The shutter/blind group with bound state.</param>
    internal static KnxShutter FromGroup(KnxGroupAddressGroup group)
        => new()
        {
            GroupName = group.GroupName,
            Floor = group.Floor,
            Room = group.Room,
            Location = group.Location,
            Orientation = group.Orientation,
            Position = ToDouble(group, ShutterFunction.POS_FB),
            SlatsPosition = ToDouble(group, ShutterFunction.POSSLATS_FB),
            Direction = ToEnum<DptUpDown>(group, ShutterFunction.DIRECTION),
            Diagnostics = ToStringValue(group, ShutterFunction.DIAG),
        };

    #region Private Helpers

    private static double? ToDouble(KnxGroupAddressGroup group, ShutterFunction function)
    {
        var child = group.Children.FirstOrDefault(c => c.Function == function.ToString());
        if (child?.Value is null)
            return null;

        return double.TryParse(child.Value, out var result) ? result : null;
    }

    private static T? ToEnum<T>(KnxGroupAddressGroup group, ShutterFunction function) where T : struct, Enum
    {
        var child = group.Children.FirstOrDefault(c => c.Function == function.ToString());
        if (child?.ValueLabel is null)
            return null;

        return Enum.TryParse<T>(child.ValueLabel, true, out var result) ? result : null;
    }

    private static string? ToStringValue(KnxGroupAddressGroup group, ShutterFunction function)
    {
        var child = group.Children.FirstOrDefault(c => c.Function == function.ToString());
        return child?.Value;
    }

    #endregion
}

namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a KNX light with decoded feedback values for each <see cref="LightingFunction"/>.
/// </summary>
public record KnxLight
{
    /// <inheritdoc cref="KnxGroupAddressGroup.GroupName"/>
    [Description("Group name (e.g. DG-LI-Office-DL-South).")]
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

    /// <inheritdoc cref="Models.LightStyle" path="/summary"/>
    [Description("Light fixture sub-type. Values: L (generic), DL (downlighter), WL (wall), PL (pendulum), LED (LED stripe).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LightStyle? Style { get; init; }

    /// <inheritdoc cref="Models.CompassOrientation" path="/summary"/>
    [Description("Compass orientation. Values: North, East, South, West.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CompassOrientation? Orientation { get; init; }

    /// <inheritdoc cref="LightingFunction.SW_FB"/>
    [Description("Current switch state. Values: Off, On.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DptSwitch? Switch { get; init; }

    /// <inheritdoc cref="LightingFunction.VFB"/>
    [Description("Current dim value as percentage (0=off, 100=full brightness).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? DimValue { get; init; }

    /// <inheritdoc cref="LightingFunction.SEQ1_FB"/>
    [Description("Sequence state. Values: Inactive, Active.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DptState? Sequence { get; init; }

    /// <inheritdoc cref="LightingFunction.RGB_FB"/>
    [Description("Current RGB colour as a hex string (e.g. ff0000 for red).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Rgb { get; init; }

    /// <inheritdoc cref="LightingFunction.HSV_FB"/>
    [Description("Current HSV colour value.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Hsv { get; init; }

    /// <inheritdoc cref="LightingFunction.LUX"/>
    [Description("Ambient brightness sensor reading in lux.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Lux { get; init; }

    /// <summary>
    /// Creates a <see cref="KnxLight"/> from a <see cref="KnxGroupAddressGroup"/> whose
    /// children have been enriched with state values.
    /// </summary>
    /// <param name="group">The lighting group with bound state.</param>
    internal static KnxLight FromGroup(KnxGroupAddressGroup group)
        => new()
        {
            GroupName = group.GroupName,
            Floor = group.Floor,
            Room = group.Room,
            Location = group.Location,
            Style = Enum.TryParse<LightStyle>(group.LightStyle, true, out var style) ? style : null,
            Orientation = group.Orientation,
            Switch = ToEnum<DptSwitch>(group, LightingFunction.SW_FB),
            DimValue = ToDouble(group, LightingFunction.VFB),
            Sequence = ToEnum<DptState>(group, LightingFunction.SEQ1_FB),
            Rgb = ToStringValue(group, LightingFunction.RGB_FB),
            Hsv = ToStringValue(group, LightingFunction.HSV_FB),
            Lux = ToDouble(group, LightingFunction.LUX),
        };

    #region Private Helpers

    private static double? ToDouble(KnxGroupAddressGroup group, LightingFunction function)
    {
        var child = group.Children.FirstOrDefault(c => c.Function == function.ToString());
        if (child?.Value is null)
            return null;

        return double.TryParse(child.Value, out var result) ? result : null;
    }

    private static T? ToEnum<T>(KnxGroupAddressGroup group, LightingFunction function) where T : struct, Enum
    {
        var child = group.Children.FirstOrDefault(c => c.Function == function.ToString());
        if (child?.ValueLabel is null)
            return null;

        return Enum.TryParse<T>(child.ValueLabel, true, out var result) ? result : null;
    }

    private static string? ToStringValue(KnxGroupAddressGroup group, LightingFunction function)
    {
        var child = group.Children.FirstOrDefault(c => c.Function == function.ToString());
        return child?.Value;
    }

    #endregion
}

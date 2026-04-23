namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a KNX HVAC zone with decoded values for each <see cref="HvacFunction"/>.
/// </summary>
public record KnxHvacZone
{
    /// <inheritdoc cref="KnxGroupAddressGroup.GroupName"/>
    [Description("Group name (e.g. EG-HZ-Kitchen).")]
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

    /// <inheritdoc cref="HvacFunction.SETP"/>
    [Description("Current temperature setpoint in degrees Celsius.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Setpoint { get; init; }

    /// <inheritdoc cref="HvacFunction.FB"/>
    [Description("Underfloor heating valve state. Values: Inactive (closed/off), Active (open/on).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DptState? Feedback { get; init; }

    /// <inheritdoc cref="HvacFunction.WINDOW"/>
    [Description("Window contact state. Values: Closed, Open.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DptWindowDoor? Window { get; init; }

    /// <inheritdoc cref="HvacFunction.TEMP"/>
    [Description("Current room temperature reading in degrees Celsius.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; init; }

    /// <inheritdoc cref="HvacFunction.OUTPUT"/>
    [Description("Valve output percentage (0=closed, 100=fully open).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Output { get; init; }

    /// <inheritdoc cref="HvacFunction.DIAG"/>
    [Description("Diagnostics string from the HVAC controller.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Diagnostics { get; init; }

    /// <inheritdoc cref="HvacFunction.HUMIDITY"/>
    [Description("Relative humidity percentage.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Humidity { get; init; }

    /// <summary>
    /// Creates a <see cref="KnxHvacZone"/> from a <see cref="KnxGroupAddressGroup"/> whose
    /// children have been enriched with state values.
    /// </summary>
    /// <param name="group">The HVAC group with bound state.</param>
    internal static KnxHvacZone FromGroup(KnxGroupAddressGroup group)
        => new()
        {
            GroupName = group.GroupName,
            Floor = group.Floor,
            Room = group.Room,
            Location = group.Location,
            Setpoint = ToDouble(group, HvacFunction.SETP),
            Feedback = ToEnum<DptState>(group, HvacFunction.FB),
            Window = ToEnum<DptWindowDoor>(group, HvacFunction.WINDOW),
            Temperature = ToDouble(group, HvacFunction.TEMP),
            Output = ToDouble(group, HvacFunction.OUTPUT),
            Diagnostics = ToStringValue(group, HvacFunction.DIAG),
            Humidity = ToDouble(group, HvacFunction.HUMIDITY),
        };

    #region Private Helpers

    private static double? ToDouble(KnxGroupAddressGroup group, HvacFunction function)
    {
        var child = group.Children.FirstOrDefault(c => c.Function == function.ToString());
        if (child?.Value is null)
            return null;

        return double.TryParse(child.Value, out var result) ? result : null;
    }

    private static T? ToEnum<T>(KnxGroupAddressGroup group, HvacFunction function) where T : struct, Enum
    {
        var child = group.Children.FirstOrDefault(c => c.Function == function.ToString());
        if (child?.ValueLabel is null)
            return null;

        return Enum.TryParse<T>(child.ValueLabel, true, out var result) ? result : null;
    }

    private static string? ToStringValue(KnxGroupAddressGroup group, HvacFunction function)
    {
        var child = group.Children.FirstOrDefault(c => c.Function == function.ToString());
        return child?.Value;
    }

    #endregion
}

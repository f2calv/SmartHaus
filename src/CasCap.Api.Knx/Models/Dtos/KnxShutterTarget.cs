namespace CasCap.Models.Dtos;

/// <summary>A shutter or blind and the position and slat angle it is being moved to.</summary>
public sealed record KnxShutterTarget
{
    /// <inheritdoc cref="KnxGroupAddressGroup.GroupName"/>
    [Description("Group name (e.g. OG-BL-FamilyBathroom-West).")]
    public required string GroupName { get; init; }

    /// <summary>Target vertical position, 0 = fully open and 100 = fully closed; <see langword="null"/> leaves it unchanged.</summary>
    [Description("Target position percentage. 0=fully open, 100=fully closed. Null when unchanged.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Position { get; init; }

    /// <summary>Target slat angle, 0 = open and 100 = closed; set only for blinds with slats.</summary>
    [Description("Target slats percentage. 0=slats open, 100=slats closed. Null when unchanged or not a blind.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Slats { get; init; }
}

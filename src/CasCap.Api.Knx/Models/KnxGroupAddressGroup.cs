namespace CasCap.Models;

/// <summary>
/// Groups related <see cref="KnxGroupAddressGroupFunction"/> records that share the same positional
/// and categorical metadata but differ only in their function suffix.
/// For example the addresses <c>OG-BL-FamilyBathroom-West-MOVE</c>,
/// <c>OG-BL-FamilyBathroom-West-POS</c> and <c>OG-BL-FamilyBathroom-West-SCENE</c>
/// all belong to the group <c>OG-BL-FamilyBathroom-West</c>.
/// </summary>
[MessagePackObject(true)]
public record KnxGroupAddressGroup
{
    /// <summary>
    /// The group address group name — all segments except the function suffix,
    /// e.g. <c>OG-BL-FamilyBathroom-West-SW</c> → <c>OG-BL-FamilyBathroom-West</c>
    /// or <c>DG-LI-Loggia-LED-SW</c> → <c>DG-LI-Loggia-LED</c>.
    /// </summary>
    [Description("Group name without the function suffix (e.g. OG-BL-FamilyBathroom-West or DG-LI-Office-DL).")]
    public required string GroupName { get; init; }

    /// <inheritdoc cref="GroupAddressCategory" path="/summary"/>
    [Description("Functional category. Values: SYS (system), ENV (environment), BI (binary contact), BL (shutter/blind), HZ (heating/HVAC), PM (presence/motion), LI (lighting), SD (power outlet).")]
    public GroupAddressCategory Category { get; init; }

    /// <inheritdoc cref="FloorType" path="/summary"/>
    [Description("Building floor. Values: KG (basement), EG (ground), OG (upper), DG (top).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FloorType? Floor { get; init; }

    /// <inheritdoc cref="RoomType" path="/summary"/>
    [Description("Room name (e.g. Kitchen, Office, LivingRoom).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RoomType? Room { get; init; }

    /// <summary>
    /// Free text location pulled from inside parentheses, e.g. <c>Entrance(FrontDoor)</c> → <c>FrontDoor</c>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("Free-text location extracted from parentheses in the address name (e.g. FrontDoor).")]
    public string? Location { get; init; }

    /// <inheritdoc cref="Models.CompassOrientation" path="/summary"/>
    [Description("Compass orientation. Values: North, East, South, West.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CompassOrientation? Orientation { get; init; }

    /// <inheritdoc cref="Models.HorizontalPosition" path="/summary"/>
    [Description("Horizontal position qualifier. Values: Left, Middle, Right, Corner.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HorizontalPosition? HorizontalPosition { get; init; }

    /// <inheritdoc cref="Models.VerticalPosition" path="/summary"/>
    [Description("Vertical position qualifier. Values: Top, Middle, Bottom.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VerticalPosition? VerticalPosition { get; init; }

    /// <inheritdoc cref="Models.LightStyle" path="/summary"/>
    [Description("Light fixture sub-type string (e.g. DL, WL, LED). Only set for LI category.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LightStyle { get; init; }

    /// <summary>
    /// Single word identifier pulled from inside square brackets, e.g. <c>SYS-[DateTime]</c> → <c>DateTime</c>.
    /// Present only when the group contains a single identifier-based address (e.g. <see cref="GroupAddressCategory.SYS"/>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("Identifier extracted from square brackets in the address name (e.g. DateTime). Only set for SYS category.")]
    public string? Identifier { get; init; }

    /// <summary>
    /// Indicates whether the device is located outside (<c>Outdoor</c>) rather than inside (<c>Indoor</c>).
    /// </summary>
    [Description("True when the device is located outside (Outdoor); false when inside.")]
    public bool IsOutside { get; init; }

    /// <summary>
    /// The individual group address functions belonging to this group.
    /// </summary>
    [Description("Individual group address functions belonging to this group, each with its current state.")]
    public IReadOnlyList<KnxGroupAddressGroupFunction> Children { get; init; } = [];
}

using Knx.Falcon;
using Knx.Falcon.ApplicationData.DatapointTypes;

namespace CasCap.Models;

/// <summary>
/// Intermediate parsing representation of a KNX Group Address parsed from the hyphenated name set-up in the ETS5 software, exported as XML and loaded into the <see cref="KnxGroupAddressXmlExport"/> object.
/// After parsing, instances are grouped into <see cref="KnxGroupAddressGroup"/> records and their
/// function-specific properties are projected into <see cref="KnxGroupAddressGroupFunction"/> records.
/// </summary>
/// <remarks>
/// <para>
/// Each KNX Group Address name is a wholly unique hyphenated string, e.g. <c>DG-LI-Office-SW</c> or
/// <c>EG-BI-Entrance(FrontDoor)-East-STATE</c>. Splitting on the hyphen produces a set of
/// segments, each carrying a specific piece of metadata:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="FloorType"/> — building floor, e.g. <c>KG</c> (basement), <c>EG</c> (ground), <c>OG</c> (upper), <c>DG</c> (top).</description></item>
///   <item><description><see cref="GroupAddressCategory"/> — functional category, e.g. <c>BI</c> (Binary), <c>LI</c> (Lighting), <c>HZ</c> (Heating), <c>BL</c> (Blinds/Shutters).</description></item>
///   <item><description><see cref="RoomType"/> — room name, e.g. <c>Entrance</c> (Hall), <c>Kitchen</c>, <c>Office</c>.</description></item>
///   <item><description><see cref="Models.CompassOrientation"/> — orientation, e.g. <c>East</c>, <c>South</c>.</description></item>
///   <item><description><see cref="Models.HorizontalPosition"/> / <see cref="Models.VerticalPosition"/> — spatial qualifiers, e.g. <c>L</c> (Left), <c>Top</c>.</description></item>
///   <item><description>Function type — category-specific operation, e.g. <c>SW</c> (Switch), <c>STATE</c> (State), <c>FB</c> (Feedback), <c>SETP</c> (Setpoint). Parsed into the corresponding enum (e.g. <see cref="LightingFunction"/>, <see cref="ShutterFunction"/>).</description></item>
///   <item><description><see cref="Models.LightStyle"/> — light fixture sub-type for <see cref="GroupAddressCategory.LI"/> addresses, e.g. <c>DL</c> (Downlighter), <c>WL</c> (Wall Light).</description></item>
///   <item><description>Parenthesised text — free-text location detail, e.g. <c>Entrance(FrontDoor)</c> → <see cref="Location"/>=<c>FrontDoor</c>.</description></item>
///   <item><description>Square-bracketed text — unique identifier, e.g. <c>SYS-[DateTime]</c> → <see cref="Identifier"/>=<c>DateTime</c>.</description></item>
///   <item><description><c>Outdoor</c>/<c>Indoor</c> — indicates whether the device is outside or inside (see <see cref="IsOutside"/>).</description></item>
///   <item><description>Any segment ending in <c>FB</c> marks the address as a feedback value (see <see cref="IsFeedback"/>).</description></item>
/// </list>
/// <para>
/// During construction every recognised segment is consumed from the sections list; after a
/// successful parse <see cref="sections"/> should be empty. Any remaining segments indicate
/// an unrecognised token in the naming convention and are surfaced for debugging.
/// </para>
/// </remarks>
[MessagePackObject(true)]
public record KnxGroupAddressParsed
{
    /// <summary>
    /// Parameterless constructor for JSON/MessagePack deserialization.
    /// </summary>
    [JsonConstructor]
    public KnxGroupAddressParsed() { }

    /// <summary>
    /// Parses a <see cref="KnxGroupAddressXml"/> into a strongly-typed <see cref="KnxGroupAddressParsed"/>
    /// by splitting the hyphenated name and consuming each recognised segment.
    /// </summary>
    /// <param name="xga">The raw XML group address element exported from ETS.</param>
    public KnxGroupAddressParsed(KnxGroupAddressXml xga)
    {
        Name = xga.Name;
        GroupAddress = xga.Address;
        Description = xga.Description;
        _sectionsList = new List<string>(Name.Split('-'));
        var category = ConsumeMatch(_sectionsList, s_categoryLookup);
        if (category is not null && category.Value != GroupAddressCategory.Unknown)
            Category = category.Value;
        else
            return;//if unrecognized then exit ctr early

        //DPST-1-1
        var dptValues = xga.DPTs!.Split('-');
        DPTs = xga.DPTs;
        Major = int.Parse(dptValues[1]);
        Minor = int.Parse(dptValues[2]);

        IsFeedback = IsFeedbackCheck(_sectionsList);
        var parsedLocation = ConsumeInnerText(_sectionsList, '(', ')');
        Identifier = ConsumeInnerText(_sectionsList, '[', ']');

        var parsedRoom = ConsumeMatch(_sectionsList, s_roomLookup);
        var parsedFloor = ConsumeMatch(_sectionsList, s_floorLookup);
        var parsedHorizontal = ConsumeMatch(_sectionsList, s_horizontalLookup);
        var parsedVertical = ConsumeMatch(_sectionsList, s_verticalLookup);
        var parsedCompass = ConsumeMatch(_sectionsList, s_compassLookup);
        var parsedIsOutside = ConsumeOutsideInside(_sectionsList);

        Floor = parsedFloor;
        Room = parsedRoom;
        Location = parsedLocation;
        CompassDirection = parsedCompass;
        HorizontalPosition = parsedHorizontal;
        VerticalPosition = parsedVertical;
        IsOutside = parsedIsOutside;

        Function = Category switch
        {
            GroupAddressCategory.BL => ConsumeFunctionType(_sectionsList, s_shutterFunctionLookup),
            GroupAddressCategory.BI => ConsumeFunctionType(_sectionsList, s_contactFunctionLookup),
            GroupAddressCategory.LI => ConsumeFunctionType(_sectionsList, s_lightingFunctionLookup),
            GroupAddressCategory.SD => ConsumeFunctionType(_sectionsList, s_powerOutletFunctionLookup),
            GroupAddressCategory.HZ => ConsumeFunctionType(_sectionsList, s_hvacFunctionLookup),
            GroupAddressCategory.SYS => ConsumeFunctionType(_sectionsList, s_systemFunctionLookup),
            GroupAddressCategory.ENV => ConsumeFunctionType(_sectionsList, s_environmentFunctionLookup),
            GroupAddressCategory.PM => ConsumeFunctionType(_sectionsList, s_presenceFunctionLookup),
            _ => null
        };
        //hack just for lighting
        LightStyle = ConsumeFunctionType(_sectionsList, s_lightStyleLookup);
    }

    #region properties
    /// <summary>
    /// Group Address Name, i.e. <c>DG-HZ-StorageRoom-SETP</c> or <c>DG-BI-STATE</c>.
    /// </summary>
    [Description("Full group address name (e.g. DG-HZ-StorageRoom-SETP or EG-LI-Entrance-DL-SW_FB).")]
    public string Name { get; set; } = default!;

    /// <summary>
    /// Group Address, e.g. <c>1/2/3</c>.
    /// </summary>
    [Description("Numeric KNX group address (e.g. 1/2/3).")]
    public string GroupAddress { get; init; } = default!;

    /// <inheritdoc cref="GroupAddressCategory" path="/summary"/>
    [Description("Functional category. Values: SYS (system), ENV (environment), BI (binary contact), BL (shutter/blind), HZ (heating/HVAC), PM (presence/motion), LI (lighting), SD (power outlet).")]
    public GroupAddressCategory Category { get; init; } = GroupAddressCategory.Unknown;

    /// <summary>
    /// Category-specific function parsed from the group address name into the corresponding enum
    /// (e.g. <see cref="LightingFunction"/>, <see cref="HvacFunction"/>), then stored as
    /// a string so that a single record type can represent all categories.
    /// </summary>
    [Description("Function name parsed from the group address name (e.g. SW_FB, POS, SETP, STATE).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Function { get; init; }

    /// <inheritdoc cref="Models.LightStyle" path="/summary"/>
    [Description("Light fixture sub-type string (e.g. DL, WL, LED). Only set for LI category.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LightStyle { get; init; }

    /// <summary>
    /// Indicates whether this group address carries a feedback value, detected by any
    /// name segment ending in <c>FB</c> (e.g. <c>SW_FB</c>, <c>POS_FB</c>).
    /// </summary>
    [Description("True when this address carries a feedback/read value (name segment ends in FB).")]
    public bool IsFeedback { get; init; }

    /// <summary>
    /// Single word identifier pulled from inside square brackets, e.g. <c>SYS-[DateTime]</c> → <c>DateTime</c>.
    /// </summary>
    [Description("Identifier extracted from square brackets in the address name (e.g. DateTime). Only set for SYS category.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Identifier { get; init; }

    /// <summary>
    /// Optional free-text description assigned to this group address in ETS.
    /// </summary>
    [Description("Optional free-text description assigned to the group address in ETS.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    /// <summary>
    /// The raw DPT string in the format <c>DPST-x-y</c>, where <c>x</c> is the main type
    /// and <c>y</c> is the sub-type (e.g. <c>DPST-1-1</c> for boolean, <c>DPST-9-1</c> for temperature).
    /// </summary>
    [Description("KNX Datapoint Type string in DPST-x-y format (e.g. DPST-1-1 for boolean, DPST-9-1 for temperature).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string DPTs { get; init; } = default!;

    /// <summary>
    /// DPT major number parsed from the <see cref="DPTs"/> string (the <c>x</c> in <c>DPST-x-y</c>).
    /// </summary>
    [Description("DPT major type number (the x in DPST-x-y).")]
    public int Major { get; init; }

    /// <summary>
    /// DPT minor/sub-type number parsed from the <see cref="DPTs"/> string (the <c>y</c> in <c>DPST-x-y</c>).
    /// </summary>
    [Description("DPT minor/sub-type number (the y in DPST-x-y).")]
    public int Minor { get; init; }

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
    [Description("Free-text location extracted from parentheses in the address name (e.g. FrontDoor).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Location { get; init; }

    /// <inheritdoc cref="Models.CompassOrientation" path="/summary"/>
    [Description("Compass orientation. Values: North, East, South, West.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CompassOrientation? CompassDirection { get; init; }

    /// <inheritdoc cref="Models.HorizontalPosition" path="/summary"/>
    [Description("Horizontal position qualifier. Values: Left, Middle, Right, Corner.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HorizontalPosition? HorizontalPosition { get; init; }

    /// <inheritdoc cref="Models.VerticalPosition" path="/summary"/>
    [Description("Vertical position qualifier. Values: Top, Middle, Bottom.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VerticalPosition? VerticalPosition { get; init; }

    /// <summary>
    /// Indicates whether the device is located outside (<c>Outdoor</c>) rather than inside (<c>Indoor</c>).
    /// </summary>
    [Description("True when the device is located outside (Outdoor); false when inside.")]
    public bool IsOutside { get; init; } = false;

    private List<string> _sectionsList = default!;
    private string[] _sections = default!;

    /// <summary>
    /// Segments remaining after parsing. An empty array indicates that every segment in the
    /// hyphenated name was successfully recognised; any leftover entries highlight unrecognised tokens.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public string[] sections
    {
        get => _sectionsList?.ToArray() ?? _sections ?? [];
        set
        {
            _sections = value;
            _sectionsList = value is not null ? new List<string>(value) : [];
        }
    }
    #endregion

    /// <summary>
    /// Gets the <see cref="DptBase"/> datapoint type instance for this data point.
    /// </summary>
    public DptBase GetDptBase() => DptFactory.Default.Get(Major, Minor);

    /// <summary>
    /// Converts an actual value to a <see cref="GroupValue"/> using this data point's type.
    /// </summary>
    /// <param name="actualValue">The value to convert (e.g. <see cref="bool"/>, <see cref="double"/>).</param>
    public GroupValue ToGroupValue(object actualValue)
    {
        var dpt = GetDptBase();
        return dpt.ToGroupValue(actualValue);
    }

    /// <summary>
    /// Parses the <see cref="GroupAddressCategory"/> from a hyphenated group address or group name
    /// by scanning the segments against the known category lookup.
    /// </summary>
    /// <param name="name">A hyphenated group address name (e.g. <c>DG-LI-Office-DL-South</c>).</param>
    /// <returns>
    /// The matched <see cref="GroupAddressCategory"/>, or <see cref="GroupAddressCategory.Unknown"/>
    /// if no recognised category segment is found.
    /// </returns>
    public static GroupAddressCategory ParseCategory(string name)
    {
        foreach (var segment in name.Split('-'))
        {
            if (s_categoryLookup.TryGetValue(segment, out var category))
                return category;
        }
        return GroupAddressCategory.Unknown;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        if (Floor is not null) sb.Append($"{Floor}");
        if (Category != GroupAddressCategory.Unknown) sb.Append($"-{Category}");
        if (Room is not null) sb.Append($"-{Room}");
        if (CompassDirection is not null) sb.Append($"-{CompassDirection}");
        if (HorizontalPosition is not null) sb.Append($"-{HorizontalPosition}");
        if (VerticalPosition is not null) sb.Append($"-{VerticalPosition}");
        if (Function is not null) sb.Append($"-{Function}");
        return sb.ToString();
    }

    #region static lookup dictionaries
    private static readonly FrozenDictionary<string, GroupAddressCategory> s_categoryLookup = new Dictionary<string, GroupAddressCategory>
    {
        [nameof(GroupAddressCategory.SYS)] = GroupAddressCategory.SYS,
        [nameof(GroupAddressCategory.ENV)] = GroupAddressCategory.ENV,
        [nameof(GroupAddressCategory.BL)] = GroupAddressCategory.BL,
        [nameof(GroupAddressCategory.BI)] = GroupAddressCategory.BI,
        [nameof(GroupAddressCategory.SD)] = GroupAddressCategory.SD,
        [nameof(GroupAddressCategory.PM)] = GroupAddressCategory.PM,
        [nameof(GroupAddressCategory.LI)] = GroupAddressCategory.LI,
        [nameof(GroupAddressCategory.HZ)] = GroupAddressCategory.HZ,
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, FloorType> s_floorLookup = new Dictionary<string, FloorType>
    {
        [nameof(FloorType.DG)] = FloorType.DG,
        [nameof(FloorType.OG)] = FloorType.OG,
        [nameof(FloorType.EG)] = FloorType.EG,
        [nameof(FloorType.KG)] = FloorType.KG,
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, RoomType> s_roomLookup = new Dictionary<string, RoomType>
    {
        [nameof(RoomType.Office)] = RoomType.Office,
        [nameof(RoomType.StorageRoom)] = RoomType.StorageRoom,
        [nameof(RoomType.Loggia)] = RoomType.Loggia,
        [nameof(RoomType.Entrance)] = RoomType.Entrance,
        [nameof(RoomType.MasterBedroom)] = RoomType.MasterBedroom,
        [nameof(RoomType.MasterBathroom)] = RoomType.MasterBathroom,
        [nameof(RoomType.FamilyBathroom)] = RoomType.FamilyBathroom,
        [nameof(RoomType.ChildRoom)] = RoomType.ChildRoom,
        [nameof(RoomType.ChildRoom1)] = RoomType.ChildRoom1,
        [nameof(RoomType.ChildRoom2)] = RoomType.ChildRoom2,
        [nameof(RoomType.Bedroom)] = RoomType.Bedroom,
        [nameof(RoomType.Kitchen)] = RoomType.Kitchen,
        [nameof(RoomType.LivingRoom)] = RoomType.LivingRoom,
        [nameof(RoomType.GuestWC)] = RoomType.GuestWC,
        [nameof(RoomType.OpenPlanLiving)] = RoomType.OpenPlanLiving,
        [nameof(RoomType.Study)] = RoomType.Study,
        [nameof(RoomType.Hallway)] = RoomType.Hallway,
        [nameof(RoomType.GuestRoom)] = RoomType.GuestRoom,
        [nameof(RoomType.GuestBathroom)] = RoomType.GuestBathroom,
        [nameof(RoomType.BoilerRoom)] = RoomType.BoilerRoom,
        [nameof(RoomType.LaundryRoom)] = RoomType.LaundryRoom,
        [nameof(RoomType.Garage)] = RoomType.Garage,
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, CompassOrientation> s_compassLookup = new Dictionary<string, CompassOrientation>
    {
        [nameof(Models.CompassOrientation.North)] = Models.CompassOrientation.North,
        [nameof(Models.CompassOrientation.East)] = Models.CompassOrientation.East,
        [nameof(Models.CompassOrientation.South)] = Models.CompassOrientation.South,
        [nameof(Models.CompassOrientation.West)] = Models.CompassOrientation.West,
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, HorizontalPosition> s_horizontalLookup = new Dictionary<string, HorizontalPosition>
    {
        [nameof(Models.HorizontalPosition.Left)] = Models.HorizontalPosition.Left,
        ["L"] = Models.HorizontalPosition.Left,
        [nameof(Models.HorizontalPosition.Middle)] = Models.HorizontalPosition.Middle,
        [nameof(Models.HorizontalPosition.Right)] = Models.HorizontalPosition.Right,
        ["R"] = Models.HorizontalPosition.Right,
        [nameof(Models.HorizontalPosition.Corner)] = Models.HorizontalPosition.Corner,
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, VerticalPosition> s_verticalLookup = new Dictionary<string, VerticalPosition>
    {
        [nameof(Models.VerticalPosition.Top)] = Models.VerticalPosition.Top,
        [nameof(Models.VerticalPosition.Middle)] = Models.VerticalPosition.Middle,
        [nameof(Models.VerticalPosition.Bottom)] = Models.VerticalPosition.Bottom,
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, ShutterFunction> s_shutterFunctionLookup =
        BuildEnumLookup<ShutterFunction>();

    private static readonly FrozenDictionary<string, ContactFunction> s_contactFunctionLookup =
        BuildEnumLookup<ContactFunction>();

    private static readonly FrozenDictionary<string, LightingFunction> s_lightingFunctionLookup =
        BuildEnumLookup<LightingFunction>();

    private static readonly FrozenDictionary<string, PowerOutletFunction> s_powerOutletFunctionLookup =
        BuildEnumLookup<PowerOutletFunction>();

    private static readonly FrozenDictionary<string, HvacFunction> s_hvacFunctionLookup =
        BuildEnumLookup<HvacFunction>();

    private static readonly FrozenDictionary<string, SystemFunction> s_systemFunctionLookup =
        BuildEnumLookup<SystemFunction>();

    private static readonly FrozenDictionary<string, EnvironmentFunction> s_environmentFunctionLookup =
        BuildEnumLookup<EnvironmentFunction>();

    private static readonly FrozenDictionary<string, PresenceFunction> s_presenceFunctionLookup =
        BuildEnumLookup<PresenceFunction>();

    private static readonly FrozenDictionary<string, LightStyle> s_lightStyleLookup =
        BuildEnumLookup<LightStyle>();
    #endregion

    #region building methods
    /// <summary>
    /// Builds a <see cref="FrozenDictionary{TKey, TValue}"/> from every named member of
    /// <typeparamref name="T"/>, keyed by the member name. Use the <paramref name="aliases"/>
    /// parameter to add German (or other) alternative keys that map to the same enum value.
    /// </summary>
    private static FrozenDictionary<string, T> BuildEnumLookup<T>(
        params ReadOnlySpan<(string alias, T value)> aliases) where T : struct, Enum
    {
        var names = Enum.GetNames<T>();
        var values = Enum.GetValues<T>();
        var dict = new Dictionary<string, T>(names.Length + aliases.Length);
        for (var i = 0; i < names.Length; i++)
            dict[names[i]] = values[i];
        foreach (var (alias, value) in aliases)
            dict[alias] = value;
        return dict.ToFrozenDictionary();
    }

    private static T? ConsumeMatch<T>(List<string> sections, FrozenDictionary<string, T> lookup)
        where T : struct
    {
        for (var i = 0; i < sections.Count; i++)
        {
            if (lookup.TryGetValue(sections[i], out var value))
            {
                sections.RemoveAt(i);
                return value;
            }
        }
        return null;
    }

    private static string? ConsumeFunctionType<T>(List<string> sections, FrozenDictionary<string, T> lookup)
        where T : struct
    {
        for (var i = 0; i < sections.Count; i++)
        {
            if (lookup.TryGetValue(sections[i], out var value))
            {
                sections.RemoveAt(i);
                return value.ToString();
            }
        }
        return null;
    }

    /// <summary>
    /// Extracts inner text from the first section containing matching delimiters.
    /// </summary>
    public static string? GetInnerText(ref string[] sections, string a, string b)
    {
        var list = new List<string>(sections);
        var result = ConsumeInnerText(list, a[0], b[0]);
        sections = list.ToArray();
        return result;
    }

    private static string? ConsumeInnerText(List<string> sections, char open, char close)
    {
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            var start = section.IndexOf(open);
            if (start < 0) continue;

            var end = section.IndexOf(close, start + 1);
            if (end < 0) continue;

            var inner = section.Substring(start + 1, end - start - 1);
            if (string.IsNullOrWhiteSpace(inner)) continue;

            sections.RemoveAt(i);
            var outer = string.Concat(section.AsSpan(0, start), section.AsSpan(end + 1));
            if (!string.IsNullOrWhiteSpace(outer))
                sections.Add(outer);
            return inner;
        }
        return null;
    }

    private static bool IsFeedbackCheck(List<string> sections)
    {
        for (var i = 0; i < sections.Count; i++)
        {
            if (sections[i].EndsWith("FB"))
                return true;
        }
        return false;
    }

    private static bool ConsumeOutsideInside(List<string> sections)
    {
        if (sections.Remove("Outdoor"))
            return true;
        sections.Remove("Indoor");
        return false;
    }
    #endregion
}

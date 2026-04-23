using Knx.Falcon;
using Knx.Falcon.ApplicationData.DatapointTypes;

namespace CasCap.Models;

/// <summary>
/// A single function within a <see cref="KnxGroupAddressGroup"/>, representing
/// one KNX group address and its datapoint type.
/// </summary>
[MessagePackObject(true)]
public record KnxGroupAddressGroupFunction
{
    /// <summary>
    /// Full group address name, e.g. <c>OG-BL-FamilyBathroom-West-MOVE</c>.
    /// </summary>
    [Description("Full group address name (e.g. OG-BL-FamilyBathroom-West-MOVE).")]
    public required string Name { get; init; }

    /// <summary>
    /// Group address, e.g. <c>1/2/3</c>.
    /// </summary>
    [Description("Numeric KNX group address (e.g. 1/2/3).")]
    public required string GroupAddress { get; init; }

    /// <summary>
    /// Category-specific function parsed from the group address name into the corresponding enum
    /// (e.g. <see cref="LightingFunction"/>, <see cref="HvacFunction"/>), then stored as
    /// a string so that a single record type can represent all categories.
    /// </summary>
    [Description("Function name parsed from the group address name (e.g. SW_FB, POS, SETP, STATE).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Function { get; init; }

    /// <summary>
    /// Indicates whether this group address carries a feedback value, detected by any
    /// name segment ending in <c>FB</c> (e.g. <c>SW_FB</c>, <c>POS_FB</c>).
    /// </summary>
    [Description("True when this address carries a feedback/read value (name segment ends in FB).")]
    public bool IsFeedback { get; init; }

    /// <summary>
    /// The raw DPT string in the format <c>DPST-x-y</c>, where <c>x</c> is the main type
    /// and <c>y</c> is the sub-type (e.g. <c>DPST-1-1</c> for boolean, <c>DPST-9-1</c> for temperature).
    /// </summary>
    [Description("KNX Datapoint Type string in DPST-x-y format (e.g. DPST-1-1 for boolean, DPST-9-1 for temperature).")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public required string DPTs { get; init; }

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

    /// <summary>
    /// The last known decoded value from the <see cref="State.Value"/> record, or <see langword="null"/> if no state has been found.
    /// </summary>
    [Description("Last known decoded value as a string (e.g. 'True', '21.5', '75'). Null if no state available.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; set; }

    /// <summary>
    /// The last known human-readable value label from the <see cref="State.ValueLabel"/> record, or <see langword="null"/> if no state has been found.
    /// </summary>
    [Description("Human-readable label for the value (e.g. 'On', 'Off', 'Open'). Null for numeric values.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ValueLabel { get; set; }

    /// <summary>
    /// The UTC timestamp of the last known <see cref="State.TimestampUtc"/> update, or <see langword="null"/> if no state has been found.
    /// </summary>
    [Description("UTC timestamp of the last state update. Null if no state available.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? TimestampUtc { get; set; }

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
}

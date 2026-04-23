namespace CasCap.Models;

/// <summary>
/// Determines the filter for group addresses, i.e. whether it is active or not.
/// </summary>
public enum GroupAddressFilter
{
    /// <summary>
    /// No filter applied — returns all group addresses.
    /// </summary>
    None,

    /// <summary>
    /// The group address is active and monitored.
    /// </summary>
    Active,

    /// <summary>
    /// The group address has not had any activity.
    /// </summary>
    Inactive,
}

/// <summary>
/// Functional category parsed from a hyphenated KNX group address name segment.
/// Each category groups related actuators/sensors (e.g. lighting, heating, shutters)
/// and determines which function-type enum is used to parse the remaining segments.
/// </summary>
/// <remarks>
/// The abbreviations originate from the German KNX naming convention and are retained
/// for backward compatibility with KNX group address exports.
/// </remarks>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GroupAddressCategory
{
    /// <summary>
    /// Unrecognised or unparsed category.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// System event, e.g. DateTime, Day/Night.
    /// </summary>
    SYS = 1,

    /// <summary>
    /// Environmental event, e.g. outdoor temperature, wind speed.
    /// </summary>
    ENV = 2,

    /// <summary>
    /// Binary contact — door, window and lock open/closed state events.
    /// </summary>
    BI = 4,

    /// <summary>
    /// Shutter and blinds control.
    /// </summary>
    BL = 8,

    /// <summary>
    /// Heating — HVAC setpoints, valve outputs, temperatures.
    /// </summary>
    HZ = 16,

    /// <summary>
    /// Presence and motion detection.
    /// </summary>
    PM = 32,

    /// <summary>
    /// Lighting — switching, dimming, RGB, scenes.
    /// </summary>
    LI = 64,

    /// <summary>
    /// Power Outlet — switchable power outlets.
    /// </summary>
    SD = 128,
}

/// <summary>
/// Horizontal spatial qualifier parsed from a group address name segment.
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HorizontalPosition
{
    /// <summary>
    /// Unknown or unspecified horizontal position.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Left.
    /// </summary>
    Left = 1,

    /// <summary>
    /// Middle.
    /// </summary>
    Middle = 2,

    /// <summary>
    /// Right.
    /// </summary>
    Right = 4,

    /// <summary>
    /// Corner.
    /// </summary>
    Corner = 8,
}

/// <summary>
/// Building floor parsed from a group address name segment.
/// Values use the German abbreviation convention from the KNX standard.
/// </summary>
/// <remarks>
/// TODO: these abbreviations are not universal outside the German-speaking world.
/// Possible future English equivalents: KG → B1, EG → GF, OG → 1F, DG → 2F or PH.
/// </remarks>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FloorType
{
    /// <summary>
    /// KG — basement / cellar floor.
    /// </summary>
    KG = 1,

    /// <summary>
    /// EG — ground floor.
    /// </summary>
    EG = 2,

    /// <summary>
    /// OG — upper floor.
    /// </summary>
    OG = 4,

    /// <summary>
    /// DG — top floor.
    /// </summary>
    DG = 8
}

/// <summary>
/// Room name parsed from a group address name segment.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RoomType
{
    /// <summary>
    /// Unknown or unspecified room.
    /// </summary>
    Unknown = 0,

    //DG

    /// <summary>
    /// Office.
    /// </summary>
    Office,

    /// <summary>
    /// Storage room.
    /// </summary>
    StorageRoom,

    /// <summary>
    /// Loggia (covered balcony).
    /// </summary>
    Loggia,

    /// <summary>
    /// Entrance / hall area — appears on multiple floors.
    /// </summary>
    Entrance,

    //OG

    /// <summary>
    /// Master bedroom.
    /// </summary>
    MasterBedroom,

    /// <summary>
    /// Master bathroom.
    /// </summary>
    MasterBathroom,

    /// <summary>
    /// Family bathroom.
    /// </summary>
    FamilyBathroom,

    /// <summary>
    /// Child's room.
    /// </summary>
    ChildRoom,

    /// <summary>
    /// Child's room 1 — for houses with multiple children's rooms.
    /// </summary>
    ChildRoom1,

    /// <summary>
    /// Child's room 2 — for houses with multiple children's rooms.
    /// </summary>
    ChildRoom2,

    /// <summary>
    /// Bedroom.
    /// </summary>
    Bedroom,

    //EG

    /// <summary>
    /// Kitchen.
    /// </summary>
    Kitchen,

    /// <summary>
    /// Living room.
    /// </summary>
    LivingRoom,

    /// <summary>
    /// Guest WC.
    /// </summary>
    GuestWC,

    /// <summary>
    /// Open-plan living area combining living room and kitchen.
    /// </summary>
    OpenPlanLiving,

    //KG

    /// <summary>
    /// Study / home office.
    /// </summary>
    Study,

    /// <summary>
    /// Hallway / corridor.
    /// </summary>
    Hallway,

    /// <summary>
    /// Guest room.
    /// </summary>
    GuestRoom,

    /// <summary>
    /// Guest bathroom.
    /// </summary>
    GuestBathroom,

    /// <summary>
    /// Boiler / plant room.
    /// </summary>
    BoilerRoom,

    /// <summary>
    /// Laundry room.
    /// </summary>
    LaundryRoom,

    //other

    /// <summary>
    /// Garage.
    /// </summary>
    Garage
}

/// <summary>
/// Compass direction parsed from a group address name segment.
/// Formerly named CardinalDirection — see <see href="https://en.wikipedia.org/wiki/Cardinal_direction"/>.
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CompassOrientation
{
    /// <summary>
    /// Unknown or unspecified direction.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// North.
    /// </summary>
    North = 1,

    /// <summary>
    /// East.
    /// </summary>
    East = 2,

    /// <summary>
    /// South.
    /// </summary>
    South = 4,

    /// <summary>
    /// West.
    /// </summary>
    West = 8
}

/// <summary>
/// Vertical spatial qualifier parsed from a group address name segment.
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VerticalPosition
{
    /// <summary>
    /// Unknown or unspecified vertical position.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Top.
    /// </summary>
    Top = 1,

    /// <summary>
    /// Middle.
    /// </summary>
    Middle = 2,

    /// <summary>
    /// Bottom.
    /// </summary>
    Bottom = 4
}

/// <summary>
/// Light fixture sub-type parsed from a <see cref="GroupAddressCategory.LI"/> group address name segment.
/// </summary>
public enum LightStyle
{
    /// <summary>
    /// Generic light.
    /// </summary>
    [Description("Generic")]
    L,

    /// <summary>
    /// Downlighter.
    /// </summary>
    [Description("Downlighter")]
    DL,

    /// <summary>
    /// Wall light.
    /// </summary>
    [Description("Wall")]
    WL,

    /// <summary>
    /// Pendulum light.
    /// </summary>
    [Description("Pendulum")]
    PL,

    /// <summary>
    /// LED stripe.
    /// </summary>
    [Description("LED Stripe")]
    LED
}

/// <summary>
/// Function type for <see cref="GroupAddressCategory.BL"/> (shutter/blind) group addresses.
/// </summary>
public enum ShutterFunction
{
    /// <summary>
    /// Unknown or unspecified shutter function.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Blind/Shutter Position, DPT 5.001 percentage (0..100%).
    /// </summary>
    POS,

    /// <summary>
    /// Blind/Shutter Position Feedback, DPT 5.001 percentage (0..100%).
    /// </summary>
    [Metric("knx.shutter.pos_fb", "%")]
    POS_FB,

    /// <summary>
    /// Shutter Slats Position, DPT 5.001 percentage (0..100%).
    /// </summary>
    POSSLATS,

    /// <summary>
    /// Shutter Slats Position Feedback, DPT 5.001 percentage (0..100%).
    /// </summary>
    [Metric("knx.shutter.posslats_fb", "%")]
    POSSLATS_FB,

    /// <summary>
    /// Direction of movement, DPT 1.008 up/down.
    /// </summary>
    [Metric("knx.shutter.direction", "1", IsBoolean = true)]
    DIRECTION,

    /// <summary>
    /// Move, DPT 1.008 up/down.
    /// </summary>
    MOVE,

    /// <summary>
    /// Scene, DPT 17.001 scene number.
    /// </summary>
    SCENE,

    /// <summary>
    /// Step movement up/down, DPT 1.007 step.
    /// </summary>
    STEP,

    /// <summary>
    /// Wind, DPT 1.005 alarm.
    /// </summary>
    WIND,

    /// <summary>
    /// Rain, DPT 1.005 alarm.
    /// </summary>
    RAIN,

    /// <summary>
    /// Diagnostics, DPT 16.000 character string (ASCII).
    /// </summary>
    DIAG,

    /// <summary>
    /// Travel Time, DPT 1.010 start/stop.
    /// </summary>
    TT,
}

/// <summary>
/// Function type for <see cref="GroupAddressCategory.BI"/> (binary contact) group addresses.
/// </summary>
public enum ContactFunction
{
    /// <summary>
    /// Unknown or unspecified contact function.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Contact State — open/closed. DPT 1.019 (window/door).
    /// </summary>
    [Metric("knx.contact.state", "1", IsBoolean = true)]
    STATE,
}

/// <summary>
/// Function type for <see cref="GroupAddressCategory.SD"/> (power outlet) group addresses.
/// </summary>
public enum PowerOutletFunction
{
    /// <summary>
    /// Unknown or unspecified power outlet function.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Power outlet switch, DPT 1.001 switch.
    /// </summary>
    SD_SW,

    /// <summary>
    /// Power outlet switch feedback, DPT 1.011 state.
    /// </summary>
    [Metric("knx.power_outlet.sd_fb", "1", IsBoolean = true)]
    SD_FB,
}

/// <summary>
/// Function type for <see cref="GroupAddressCategory.LI"/> (lighting) group addresses.
/// </summary>
public enum LightingFunction
{
    /// <summary>
    /// Unknown or unspecified lighting function.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Switch on/off, DPT 1.001 switch.
    /// </summary>
    SW,

    /// <summary>
    /// Switch feedback, DPT 1.001 switch.
    /// </summary>
    [Metric("knx.lighting.sw_fb", "1", IsBoolean = true)]
    SW_FB,

    /// <summary>
    /// Dimming (relative), DPT 3.007 dimming control.
    /// </summary>
    DIM,

    /// <summary>
    /// Absolute dimming value, DPT 5.001 percentage (0..100%).
    /// </summary>
    VAL,

    /// <summary>
    /// Dimming value feedback, DPT 5.001 percentage (0..100%).
    /// </summary>
    [Metric("knx.lighting.vfb", "%")]
    VFB,

    /// <summary>
    /// Stairway function (timed on/off), DPT 1.001 switch.
    /// </summary>
    STAIRWAY,

    /// <summary>
    /// Sequence #1, DPT 1.010 start/stop.
    /// </summary>
    SEQ1,

    /// <summary>
    /// Sequence #1 feedback, DPT 1.011 state.
    /// </summary>
    SEQ1_FB,

    /// <summary>
    /// Bit Scene #1, DPT 1.001 switch.
    /// </summary>
    BITSCENE1,

    /// <summary>
    /// Red Green Blue colour control, DPT 232.600 RGB value 3x(0..255).
    /// </summary>
    RGB,

    /// <summary>
    /// Red Green Blue feedback, DPT 232.600 RGB value 3x(0..255).
    /// </summary>
    RGB_FB,

    /// <summary>
    /// Red Green Blue dimming, DPT 3.007 dimming control.
    /// </summary>
    RGB_DIM,

    /// <summary>
    /// HSV colour control, DPT 3.007 dimming control.
    /// </summary>
    HSV,

    /// <summary>
    /// HSV feedback, DPT 232.600 RGB value 3x(0..255).
    /// </summary>
    HSV_FB,

    /// <summary>
    /// Scene recall/store, DPT 18.001 scene control.
    /// </summary>
    SCENE,

    /// <summary>
    /// Brightness / lux level, DPT 9.004 lux.
    /// </summary>
    [Metric("knx.lighting.lux", "lx")]
    LUX,

    /// <summary>
    /// Lock/block output, DPT 1.003 enable.
    /// </summary>
    LOCK
}

/// <summary>
/// Function type for <see cref="GroupAddressCategory.SYS"/> (system) group addresses.
/// </summary>
public enum SystemFunction
{
    /// <summary>
    /// Unknown or unspecified system function.
    /// </summary>
    Unknown = 0,
}

/// <summary>
/// Function type for <see cref="GroupAddressCategory.ENV"/> (environment) group addresses.
/// </summary>
public enum EnvironmentFunction
{
    /// <summary>
    /// Unknown or unspecified environment function.
    /// </summary>
    Unknown = 0,
}

/// <summary>
/// Function type for <see cref="GroupAddressCategory.PM"/> (presence/motion) group addresses.
/// </summary>
public enum PresenceFunction
{
    /// <summary>
    /// Standard DPT 1.018 — Occupancy=1, NoOccupancy=0.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Inverted logic, DPT 1.018 — Occupancy=0, NoOccupancy=1.
    /// </summary>
    INVERT = 0,
}

/// <summary>
/// Function type for <see cref="GroupAddressCategory.HZ"/> (heating/HVAC) group addresses.
/// </summary>
public enum HvacFunction
{
    /// <summary>
    /// Unknown or unspecified HVAC function.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Read current setpoint, DPT 9.001 temperature (°C).
    /// </summary>
    [Metric("knx.hvac.setp", "Cel")]
    SETP,

    /// <summary>
    /// Update setpoint, DPT 9.001 temperature (°C).
    /// </summary>
    SETP_UPDATE,

    /// <summary>
    /// State/feedback of underfloor heating valves, DPT 1.011 state.
    /// </summary>
    [Metric("knx.hvac.fb", "1", IsBoolean = true)]
    FB,

    /// <summary>
    /// Change setpoint via offset, DPT 1.007 step.
    /// </summary>
    OFFSET,

    /// <summary>
    /// Window contact (open/closed), DPT 1.019 window/door.
    /// </summary>
    [Metric("knx.hvac.window", "1", IsBoolean = true)]
    WINDOW,

    /// <summary>
    /// Temperature reading, DPT 9.001 temperature (°C).
    /// </summary>
    /// <remarks>
    /// Primary value most commonly used for heating control.
    /// </remarks>
    [Metric("knx.hvac.temp", "Cel")]
    TEMP,

    /// <summary>
    /// Valve output reading, DPT 5.001 percentage (0..100%).
    /// </summary>
    [Metric("knx.hvac.output", "%")]
    OUTPUT,

    /// <summary>
    /// Diagnostics, DPT 16.001 character string (ISO 8859-1).
    /// </summary>
    DIAG,

    ///// <summary>
    ///// Heating mode (comfort, standby, economy, protection), DPT 20.102 HVAC mode.
    ///// </summary>
    //MODE,

    /// <summary>
    /// Relative humidity, DPT 9.007 percentage (%).
    /// </summary>
    [Metric("knx.hvac.humidity", "%")]
    HUMIDITY,
}

/// <summary>
/// Decoded values for KNX DPT 1.001 — switch (off/on).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DptSwitch
{
    /// <summary>
    /// DPT 1.001 value 0 — off.
    /// </summary>
    Off,

    /// <summary>
    /// DPT 1.001 value 1 — on.
    /// </summary>
    On,
}

/// <summary>
/// Decoded values for KNX DPT 1.008 — up/down direction.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DptUpDown
{
    /// <summary>
    /// DPT 1.008 value 0 — up.
    /// </summary>
    Up,

    /// <summary>
    /// DPT 1.008 value 1 — down.
    /// </summary>
    Down,
}

/// <summary>
/// Decoded values for KNX DPT 1.011 — binary state (inactive/active).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DptState
{
    /// <summary>
    /// DPT 1.011 value 0 — inactive.
    /// </summary>
    Inactive,

    /// <summary>
    /// DPT 1.011 value 1 — active.
    /// </summary>
    Active,
}

/// <summary>
/// Decoded values for KNX DPT 1.019 — window/door contact (closed/open).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DptWindowDoor
{
    /// <summary>
    /// DPT 1.019 value 0 — closed.
    /// </summary>
    Closed,

    /// <summary>
    /// DPT 1.019 value 1 — open.
    /// </summary>
    Open,
}

/// <summary>
/// Indicates the result of attempting to change a KNX group address state.
/// </summary>
public enum StateChangeOutcome
{
    /// <summary>
    /// The state change request has been validated and queued for background processing.
    /// The value will be sent to the KNX bus and the feedback address polled asynchronously.
    /// </summary>
    Queued,

    /// <summary>
    /// The request was a dry run — the state change was resolved and validated but not
    /// queued for execution. The <see cref="KnxStateChangeResult.State"/> contains the
    /// current feedback value that would have been affected.
    /// </summary>
    DryRun,

    /// <summary>
    /// The state was successfully changed to the desired value (set by the background processor).
    /// </summary>
    Changed,

    /// <summary>
    /// The group address already had the desired value — no command was sent.
    /// </summary>
    AlreadyAtDesiredValue,

    /// <summary>
    /// The command was sent but the feedback address did not reach the desired value within the polling window.
    /// </summary>
    NotChanged,

    /// <summary>
    /// The command or feedback group address could not be found.
    /// </summary>
    NotFound,

    /// <summary>
    /// No value was provided in the request.
    /// </summary>
    NoValueProvided,
}

/// <summary>Pre-defined shutter/blind positions used by the holiday timer and automation rules.</summary>
public enum ShutterScene
{
    /// <summary>Fully open (0 %). Applies to both shutters and blinds.</summary>
    Opened = 0,

    /// <summary>Fully closed (100 %). Applies to both shutters and blinds.</summary>
    Closed = 1,

    /// <summary>Closed 100 %, slats tilted to 70 %. Blinds only.</summary>
    Closed100pct_Slats70pct = 2,

    /// <summary>Closed 100 %, slats tilted to 40 %. Blinds only.</summary>
    Closed100pct_Slats40pct = 3,

    /// <summary>Closed 100 %, slats tilted to 20 %. Blinds only.</summary>
    Closed100pct_Slats20pct = 4,
    //5
    //6

    /// <summary>Closed to 85 %. Shutters only.</summary>
    Closed85pct = 7,
}

/// <summary>
/// Determines the transport mechanism used for KNX telegram brokering between
/// producer and consumer services.
/// </summary>
public enum TelegramBrokerMode
{
    /// <summary>
    /// In-process <see cref="System.Threading.Channels.Channel{T}"/> — local development only.
    /// </summary>
    /// <remarks>
    /// Because the channel is in-process, external callers (MCP tools, Agents) running in a
    /// different pod cannot publish outgoing telegrams. Use only when all services and callers
    /// run in the same process.
    /// </remarks>
    Channel,

    /// <summary>
    /// Redis streams — required for all Kubernetes deployments and external MCP/Agent access.
    /// </summary>
    /// <remarks>
    /// Decouples the "accept command" step from the "send to bus" step via a Redis stream,
    /// so any pod (or external service) can publish an outgoing telegram and the leader's
    /// <see cref="CasCap.Services.KnxSenderBgService"/> consumes it. Also enables cross-pod
    /// incoming telegram distribution in <see cref="ShardingMode.Partitioned"/> mode.
    /// </remarks>
    Redis,
}

/// <summary>Determines how KNX bus monitoring pods are distributed across KNX lines.</summary>
public enum ShardingMode
{
    /// <summary>
    /// A single active pod processes telegrams from all configured lines.
    /// Additional replicas remain on standby via Redlock-based leadership election
    /// and take over immediately if the active pod crashes.
    /// </summary>
    Unified,

    /// <summary>
    /// Each pod in a Kubernetes <c>StatefulSet</c> connects to exactly one KNX line,
    /// determined by mapping the pod ordinal to an entry in <see cref="KnxConfig.TunnelingAreaLineFilter"/>.
    /// The replica count must equal the number of configured lines.
    /// Only valid when <see cref="KnxConfig.ServiceFamily"/> is
    /// <see cref="Knx.Falcon.KnxnetIp.ServiceFamily.Tunneling"/>.
    /// </summary>
    Partitioned,
}

namespace CasCap.Abstractions;

/// <summary>
/// Defines the public query and command operations exposed by the KNX bus service.
/// </summary>
public interface IKnxQueryService
{
    /// <summary>
    /// Sends a value to the KNX bus for a given group address name and polls for state change confirmation.
    /// </summary>
    /// <param name="request">The <see cref="KnxStateChangeRequest"/> containing the group address name and value to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="State"/> object if the group address was found and the value was queued, null otherwise.</returns>
    Task<State?> Send2Bus(KnxStateChangeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the state of a KNX lighting group address, resolving the appropriate command and feedback function pair.
    /// </summary>
    /// <param name="request">The <see cref="KnxLightStateChangeRequest"/> containing the group address base name and desired state.</param>
    /// <param name="dryRun">When <see langword="true"/>, resolves and validates the request without sending commands to the bus.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="KnxStateChangeResponse"/> containing the outcome and state of each affected group address.</returns>
    Task<KnxStateChangeResponse> SetLightState(KnxLightStateChangeRequest request, bool dryRun = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the vertical position and/or slats of a KNX blind or shutter.
    /// </summary>
    /// <param name="request">The <see cref="KnxShutterStateChangeRequest"/> containing the group address base name and desired position/slats values.</param>
    /// <param name="dryRun">When <see langword="true"/>, resolves and validates the request without sending commands to the bus.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="KnxStateChangeResponse"/> containing the outcome and state of each affected group address.</returns>
    Task<KnxStateChangeResponse> SetShutterState(KnxShutterStateChangeRequest request, bool dryRun = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches a KNX power outlet on or off.
    /// </summary>
    /// <param name="request">The <see cref="KnxPowerOutletStateChangeRequest"/> containing the group address base name and desired on/off state.</param>
    /// <param name="dryRun">When <see langword="true"/>, resolves and validates the request without sending commands to the bus.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="KnxStateChangeResponse"/> containing the outcome and state of the affected group address.</returns>
    Task<KnxStateChangeResponse> SetPowerOutletState(KnxPowerOutletStateChangeRequest request, bool dryRun = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adjusts the temperature setpoint of a KNX HVAC zone.
    /// </summary>
    /// <param name="request">The <see cref="KnxHvacZoneStateChangeRequest"/> containing the group address base name and desired setpoint.</param>
    /// <param name="dryRun">When <see langword="true"/>, resolves and validates the request without sending commands to the bus.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="KnxStateChangeResponse"/> containing the outcome and state of the affected group address.</returns>
    Task<KnxStateChangeResponse> SetHvacState(KnxHvacZoneStateChangeRequest request, bool dryRun = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a collection of <see cref="KnxGroupAddressParsed"/> objects, optionally filtered by the specified <see cref="GroupAddressFilter"/>.
    /// </summary>
    /// <param name="groupAddressFilter"><inheritdoc cref="GroupAddressFilter" path="/summary"/></param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IEnumerable<KnxGroupAddressParsed>> GetGroupAddresses(GroupAddressFilter groupAddressFilter = GroupAddressFilter.None, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the raw unfiltered <see cref="KnxGroupAddressXml"/> entries from the ETS XML export.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<KnxGroupAddressXml>> GetGroupAddressesRaw(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all parsed group addresses grouped into <see cref="KnxGroupAddressGroup"/> records, enriched with current state values.
    /// </summary>
    /// <param name="groupAddressFilter"><inheritdoc cref="GroupAddressFilter" path="/summary"/></param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<KnxGroupAddressGroup>> GetGroupAddressesGrouped(GroupAddressFilter groupAddressFilter = GroupAddressFilter.None, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns off all currently switched-on lights in the house.
    /// </summary>
    /// <returns>An array of group names that were affected by the change.</returns>
    Task<string[]> TurnAllLightsOff(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens all closed shutters in the house (sets position to 0%).
    /// </summary>
    /// <returns>An array of group names that were affected by the change.</returns>
    Task<string[]> OpenAllShutters(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes all open shutters in the house (sets position to 100%).
    /// </summary>
    /// <returns>An array of group names that were affected by the change.</returns>
    Task<string[]> CloseAllShutters(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the names of group addresses filtered by the specified criteria: <see cref="GroupAddressCategory"/>, <see cref="FloorType"/> and <see cref="CompassOrientation"/>.
    /// </summary>
    /// <param name="category">The <see cref="GroupAddressCategory"/> to filter by.</param>
    /// <param name="floor">The <see cref="FloorType"/> to filter by (e.g. EG, OG, DG, KG).</param>
    /// <param name="orientation">The <see cref="CompassOrientation"/> to filter by (e.g. North, East, South, West).</param>
    /// <param name="function">The group address function to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching group address names.</returns>
    Task<List<string>> FilterGroupAddresses(string? category = null, string? floor = null, string? orientation = null, string? function = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the distinct floors in the house ordered from top to bottom (DG, OG, EG, KG).
    /// </summary>
    Task<List<KnxRoom>> ListFloors(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the distinct rooms in the house grouped by floor, ordered from top to bottom.
    /// </summary>
    Task<List<KnxRoom>> ListRooms(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all shutters/blinds in the house with current state, optionally filtered by room.
    /// </summary>
    /// <param name="room">Optional room name to filter by (e.g. Kitchen, LivingRoom).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<KnxShutter>> ListShutters(string? room = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current state of a specific shutter/blind including position, slats position, direction and diagnostics.
    /// </summary>
    /// <param name="groupName">The shutter group name to look up (e.g. <c>OG-BL-FamilyBathroom-West</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<KnxShutter?> GetShutter(string groupName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all lights in the house with current state, optionally filtered by room.
    /// </summary>
    /// <param name="room">Optional room name to filter by (e.g. Kitchen, LivingRoom).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<KnxLight>> ListLights(string? room = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current state of a specific light including switch state, dimming value, RGB, HSV and lux values.
    /// </summary>
    /// <param name="groupName">The lighting group name to look up (e.g. <c>DG-LI-Office-DL-South</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<KnxLight?> GetLight(string groupName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all switchable power outlets in the house with current state, optionally filtered by room.
    /// </summary>
    /// <param name="room">Optional room name to filter by (e.g. Office, LivingRoom).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<KnxPowerOutlet>> ListPowerOutlets(string? room = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current state of a specific power outlet.
    /// </summary>
    /// <param name="groupName">The power outlet group name to look up (e.g. <c>DG-SD-Office</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<KnxPowerOutlet?> GetPowerOutlet(string groupName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether a group name exists and returns the matching <see cref="KnxGroupAddressGroup"/> with all its child functions.
    /// </summary>
    /// <param name="groupName">The group name to validate (e.g. <c>OG-BL-FamilyBathroom-West</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<KnxGroupAddressGroup?> ValidateGroupName(string groupName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether a full group address name or numeric address exists and returns the matching <see cref="KnxGroupAddressParsed"/>.
    /// </summary>
    /// <param name="groupAddress">The full group address name (e.g. <c>EG-LI-Entrance-DL-SW</c>) or numeric address (e.g. <c>1/2/3</c>) to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<KnxGroupAddressParsed?> ValidateGroupAddress(string groupAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the magic test number 1337. Used to verify MCP tool connectivity.
    /// </summary>
    int LetsTestXmlComments();

    /// <summary>
    /// Returns all KNX group address states including values and timestamps.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary keyed by group address name.</returns>
    Task<Dictionary<string, State>> GetAllState(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all lighting and the current state of each light, optionally filtered by floor and compass orientation.
    /// </summary>
    /// <param name="floor">The <see cref="FloorType"/> to filter by (e.g. EG, OG, DG, KG).</param>
    /// <param name="orientation">The <see cref="CompassOrientation"/> to filter by (e.g. North, East, South, West).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Dictionary<string, State?>> GetAllLighting(string? floor = null, string? orientation = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all HVAC zones in the house with current state, optionally filtered by room.
    /// </summary>
    /// <param name="room">Optional room name to filter by (e.g. Office, LivingRoom).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<KnxHvacZone>> ListHvacZones(string? room = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current state of a specific HVAC zone including setpoint, temperature and valve feedback values.
    /// </summary>
    /// <param name="groupName">The HVAC group name to look up (e.g. <c>EG-HZ-Kitchen</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<KnxHvacZone?> GetHvacZone(string groupName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a value directly to the KNX bus for a given group address name without feedback polling.
    /// Used for fire-and-forget direct writes (e.g. from automation background services).
    /// </summary>
    /// <param name="groupAddressName">The full group address name (e.g. <c>SYS-[Night_Day]</c>).</param>
    /// <param name="value">The value to send to the bus.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendDirectAsync(string groupAddressName, object value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a single command-feedback pair to the KNX bus and polls the feedback address until
    /// the desired value is confirmed or the polling timeout is reached.
    /// </summary>
    /// <param name="groupName">The base group address name (e.g. <c>EG-LI-Entrance-DL</c>).</param>
    /// <param name="function">The command function suffix (e.g. <c>SW</c>).</param>
    /// <param name="feedback">The feedback function suffix (e.g. <c>SW_FB</c>).</param>
    /// <param name="value">The value to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<KnxStateChangeResult> SendValueAsync(string groupName, object function, object feedback, object value, CancellationToken cancellationToken = default);
}

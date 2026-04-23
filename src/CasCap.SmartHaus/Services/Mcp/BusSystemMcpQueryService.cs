namespace CasCap.Services;

/// <summary>
/// MCP wrapper for <see cref="IKnxQueryService"/> that exposes bus system operations as MCP tools.
/// </summary>
[McpServerToolType]
public partial class BusSystemMcpQueryService(IKnxQueryService knxQuerySvc, IKnxState knxState)
{
    //TODO: I dont like these group addresses hardcoded here, need to change this
    const string FrontDoorContactGroupName = "EG-BI-Entrance(FrontDoor)-East-STATE";

    /// <inheritdoc cref="IKnxQueryService.SetShutterState"/>
    [McpServerTool]
    [Description("Moves a single shutter or blind to a target position and/or slat angle. IMPORTANT: KNX position convention is 0=fully open, 100=fully closed.")]
    public Task<KnxStateChangeResponse> ChangeHouseShutterState(
        [Description("GroupName (BL category) plus at least one of: VPosition or Slats. IMPORTANT: 0=fully open, 100=fully closed (KNX convention).")]
        KnxShutterStateChangeRequest request,
        [Description("When true, validates without sending commands.")]
        bool dryRun = false,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.SetShutterState(request, dryRun, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.SetPowerOutletState"/>
    [McpServerTool]
    [Description("Switches a single power outlet on or off.")]
    public Task<KnxStateChangeResponse> ChangeHousePowerOutletState(
        [Description("GroupName (SD category) and IsOn (true/false).")]
        KnxPowerOutletStateChangeRequest request,
        [Description("When true, validates without sending commands.")]
        bool dryRun = false,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.SetPowerOutletState(request, dryRun, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.SetHvacState"/>
    [McpServerTool]
    [Description("Sets the target temperature of a single heating zone.")]
    public Task<KnxStateChangeResponse> ChangeHouseHeatingZone(
        [Description("GroupName (HZ category) and SetpointAdjust in °C (14–25).")]
        KnxHvacZoneStateChangeRequest request,
        [Description("When true, validates without sending commands.")]
        bool dryRun = false,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.SetHvacState(request, dryRun, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.GetGroupAddressesGrouped"/>
    [McpServerTool]
    [Description("Low-level listing of all KNX group addresses with current values, grouped by location and category.")]
    public Task<List<KnxGroupAddressGroup>> GetHouseGroupAddresses(
        [Description("Values: None (all), Active (have a current value), Inactive (no current value).")]
        GroupAddressFilter groupAddressFilter = GroupAddressFilter.None,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.GetGroupAddressesGrouped(groupAddressFilter, cancellationToken);

    /// <summary>Gets the current open/closed state of the front door contact sensor.</summary>
    [McpServerTool]
    [Description("Gets the current open/closed state of the front door via a magnetic contact sensor. Reports whether the door is physically open or closed — this is NOT a lock sensor and cannot detect locked/unlocked.")]
    public async Task<FrontDoorContactState> GetHouseFrontDoorState(CancellationToken cancellationToken = default)
    {
        var state = await knxState.GetKnxState(FrontDoorContactGroupName, cancellationToken);
        var isOpen = state?.ValueLabel?.Equals("True", StringComparison.OrdinalIgnoreCase) == true
            || state?.Value?.Equals("True", StringComparison.OrdinalIgnoreCase) == true
            || state?.Value == "1";
        return new FrontDoorContactState
        {
            IsOpen = isOpen,
            State = isOpen ? "Open" : "Closed",
            LastUpdated = state?.TimestampUtc ?? DateTime.MinValue,
            Contact = new KnxContact
            {
                GroupName = FrontDoorContactGroupName,
                Floor = FloorType.EG,
                Location = "FrontDoor",
                Orientation = CompassOrientation.East,
                State = isOpen ? DptState.Active : DptState.Inactive,
            },
        };
    }

    /// <inheritdoc cref="IKnxQueryService.OpenAllShutters"/>
    [McpServerTool]
    [Description("Opens every shutter and blind that is not already fully open (sets position to 0).")]
    public Task<string[]> OpenAllHouseShutters(CancellationToken cancellationToken = default)
        => knxQuerySvc.OpenAllShutters(cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.CloseAllShutters"/>
    [McpServerTool]
    [Description("Closes every shutter and blind that is not already fully closed (sets position to 100).")]
    public Task<string[]> CloseAllHouseShutters(CancellationToken cancellationToken = default)
        => knxQuerySvc.CloseAllShutters(cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.FilterGroupAddresses"/>
    [McpServerTool]
    [Description("Searches KNX group address names by category, floor, orientation and/or function.")]
    public Task<List<string>> SearchHouseGroupAddresses(
        [Description("Values: SYS, ENV, BI (door/window), BL (shutter), HZ (heating), PM (motion), LI (lighting), SD (outlet).")]
        string? category = null,
        [Description("Values: KG (basement), EG (ground), OG (upper), DG (top).")]
        string? floor = null,
        [Description("Values: North, East, South, West.")]
        string? orientation = null,
        [Description("Exact function enum name, e.g. SW_FB, VFB, POS_FB.")]
        string? function = null,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.FilterGroupAddresses(category, floor, orientation, function, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.ListFloors"/>
    [McpServerTool]
    [Description("Lists the floors (storeys) of the house from top to bottom.")]
    public Task<List<KnxRoom>> GetHouseFloors(CancellationToken cancellationToken = default)
        => knxQuerySvc.ListFloors(cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.ListRooms"/>
    [McpServerTool]
    [Description("Lists every room in the house grouped by floor.")]
    public Task<List<KnxRoom>> GetHouseRooms(CancellationToken cancellationToken = default)
        => knxQuerySvc.ListRooms(cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.ListShutters"/>
    [McpServerTool]
    [Description("Returns the current open/closed status and position of all shutters and blinds, optionally filtered by room. Position: 0=fully open, 100=fully closed (KNX convention).")]
    public Task<List<KnxShutter>> GetHouseShutterStates(
        [Description("Room filter (e.g. Kitchen, Office, LivingRoom).")]
        string? room = null,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.ListShutters(room, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.GetShutter"/>
    [McpServerTool]
    [Description("Returns the current status of a single shutter — open/closed position, slats, direction and diagnostics. Position: 0=fully open, 100=fully closed (KNX convention).")]
    public Task<KnxShutter?> GetHouseShutterState(
        [Description("e.g. OG-BL-FamilyBathroom-West.")]
        string groupName,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.GetShutter(groupName, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.ListPowerOutlets"/>
    [McpServerTool]
    [Description("Lists all power outlets with current on/off state, optionally filtered by room.")]
    public Task<List<KnxPowerOutlet>> GetHousePowerOutlets(
        [Description("Room filter (e.g. Kitchen, Office, LivingRoom).")]
        string? room = null,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.ListPowerOutlets(room, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.GetPowerOutlet"/>
    [McpServerTool]
    [Description("Gets one power outlet by group name — on/off state.")]
    public Task<KnxPowerOutlet?> GetHousePowerOutlet(
        [Description("e.g. DG-SD-Office.")]
        string groupName,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.GetPowerOutlet(groupName, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.ValidateGroupName"/>
    [McpServerTool]
    [Description("Checks whether a KNX group name exists and returns its child functions.")]
    public Task<KnxGroupAddressGroup?> ValidateGroupName(
        [Description("e.g. OG-BL-FamilyBathroom-West or EG-LI-Entrance-DL.")]
        string groupName,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.ValidateGroupName(groupName, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.ValidateGroupAddress"/>
    [McpServerTool]
    [Description("Checks whether a KNX group address exists by name or numeric address.")]
    public Task<KnxGroupAddressParsed?> ValidateGroupAddress(
        [Description("Name (e.g. EG-LI-Entrance-DL-SW) or numeric (e.g. 1/2/3).")]
        string groupAddress,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.ValidateGroupAddress(groupAddress, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.LetsTestXmlComments"/>
    [McpServerTool]
    [Description("Connectivity test — returns 1337 on success.")]
    public int TestBusConnectivity() => knxQuerySvc.LetsTestXmlComments();

    /// <inheritdoc cref="IKnxQueryService.ListHvacZones"/>
    [McpServerTool]
    [Description("Lists all heating zones with setpoint, temperature and valve state, optionally filtered by room.")]
    public Task<List<KnxHvacZone>> GetHouseHeatingZones(
        [Description("Room filter (e.g. Kitchen, Office, LivingRoom).")]
        string? room = null,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.ListHvacZones(room, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.GetHvacZone"/>
    [McpServerTool]
    [Description("Gets one heating zone by group name — setpoint, temperature and valve state.")]
    public Task<KnxHvacZone?> GetHouseHeatingZone(
        [Description("e.g. EG-HZ-Kitchen.")]
        string groupName,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.GetHvacZone(groupName, cancellationToken);
}

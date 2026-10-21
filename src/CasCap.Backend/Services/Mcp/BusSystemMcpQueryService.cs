using ModelContextProtocol;

namespace CasCap.Services;

/// <summary>
/// MCP wrapper for <see cref="IKnxQueryService"/> that exposes bus system operations as MCP tools.
/// </summary>
[McpServerToolType]
public sealed partial class BusSystemMcpQueryService(
    IKnxQueryService knxQuerySvc,
    IKnxState knxState,
    ITrackedOperationService trackedOperations,
    IOptions<KnxConfig> knxConfig)
{
    /// <summary>Starts moving one shutter or blind; completion is reported later in the chat.</summary>
    [McpServerTool(ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Starts moving a single shutter or blind to a target position and/or slat angle and returns immediately; completion is posted to the chat later. IMPORTANT: KNX position convention is 0=fully open, 100=fully closed. When closing a blind for privacy, also set Slats=100.")]
    public async Task<ShutterMovementStarted> ChangeHouseShutterState(
        [Description("GroupName (BL category) plus at least one of: VPosition or Slats. IMPORTANT: 0=fully open, 100=fully closed (KNX convention).")]
        KnxShutterStateChangeRequest request,
        [Description("When true, validates without sending commands.")]
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        var groups = await knxQuerySvc.GetGroupAddressesGrouped(GroupAddressFilter.None, cancellationToken);
        if (ValidateShutterTarget(groups, request) is { } error)
            throw new McpException(error);

        if (dryRun)
        {
            var validation = await knxQuerySvc.SetShutterState(request, dryRun: true, cancellationToken);
            return new ShutterMovementStarted
            {
                Shutters = [],
                Message = $"Dry run only, nothing was sent: {string.Join(", ", validation.Results.Select(r => $"{r.GroupAddress}={r.Outcome}"))}.",
            };
        }

        // Slats move only after vertical travel stops, so the tracked operation sends them later.
        await knxQuerySvc.MoveShutter(request with { Slats = request.VPosition is null ? request.Slats : null }, cancellationToken);
        KnxShutterTarget target = new() { GroupName = request.GroupName, Position = request.VPosition, Slats = request.Slats };
        return Track($"Moving {request.GroupName}", [target], "Movement started for 1 shutter");
    }

    /// <summary>
    /// Returns an error when the request names no blind with the commands it needs, so a group that
    /// moves several blinds at once is rejected instead of reporting a movement that never starts.
    /// </summary>
    internal static string? ValidateShutterTarget(IEnumerable<KnxGroupAddressGroup> groups, KnxShutterStateChangeRequest request)
    {
        var blinds = groups.Where(g => g.Category == GroupAddressCategory.BL).ToList();
        var group = blinds.Find(g => g.GroupName == request.GroupName);
        string[] required = [.. new[] {
            request.VPosition is null ? null : nameof(ShutterFunction.POS),
            request.Slats is null ? null : nameof(ShutterFunction.POSSLATS),
        }.OfType<string>()];
        var missing = group is null
            ? required
            : [.. required.Where(f => !group.Children.Any(c => c.Function == f))];
        if (missing.Length == 0)
            return null;

        var individual = blinds
            .Where(g => g.GroupName.StartsWith(request.GroupName + "-", StringComparison.Ordinal)
                && required.All(f => g.Children.Any(c => c.Function == f)))
            .Select(g => g.GroupName)
            .ToList();
        var reason = group is null
            ? $"'{request.GroupName}' is not a known blind."
            : $"'{request.GroupName}' has no {string.Join(" or ", missing)} address, so it cannot be moved to a position.";
        return individual.Count > 0
            ? $"{reason} Move each blind separately: {string.Join(", ", individual)}."
            : $"{reason} Use get_house_shutter_states to find blinds with position control.";
    }

    /// <inheritdoc cref="IKnxQueryService.SetPowerOutletState"/>
    [McpServerTool(ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Switches a single power outlet on or off.")]
    public Task<KnxStateChangeResponse> ChangeHousePowerOutletState(
        [Description("GroupName (SD category) and IsOn (true/false).")]
        KnxPowerOutletStateChangeRequest request,
        [Description("When true, validates without sending commands.")]
        bool dryRun = false,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.SetPowerOutletState(request, dryRun, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.SetHvacState"/>
    [McpServerTool(ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Sets the target temperature of a single heating zone.")]
    public Task<KnxStateChangeResponse> ChangeHouseHeatingZone(
        [Description("GroupName (HZ category) and SetpointAdjust in °C (14–25).")]
        KnxHvacZoneStateChangeRequest request,
        [Description("When true, validates without sending commands.")]
        bool dryRun = false,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.SetHvacState(request, dryRun, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.GetGroupAddressesGrouped"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Low-level listing of all KNX group addresses with current values, grouped by location and category.")]
    public Task<List<KnxGroupAddressGroup>> GetHouseGroupAddresses(
        [Description("Values: None (all), Active (have a current value), Inactive (no current value).")]
        GroupAddressFilter groupAddressFilter = GroupAddressFilter.None,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.GetGroupAddressesGrouped(groupAddressFilter, cancellationToken);

    /// <summary>Gets the current open/closed state of the front door contact sensor.</summary>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Gets the current open/closed state of the front door via a magnetic contact sensor. Reports whether the door is physically open or closed — this is NOT a lock sensor and cannot detect locked/unlocked.")]
    public async Task<FrontDoorContactState> GetHouseFrontDoorState(CancellationToken cancellationToken = default)
    {
        var groupName = knxConfig.Value.FrontDoorContactGroupName;
        var state = await knxState.GetKnxState(groupName, cancellationToken);
        var isOpen = state?.ValueLabel?.Equals("True", StringComparison.OrdinalIgnoreCase) == true
            || state?.Value?.Equals("True", StringComparison.OrdinalIgnoreCase) == true
            || state?.Value == "1";
        var contacts = await knxQuerySvc.GetContactSummary(cancellationToken: cancellationToken);
        return new FrontDoorContactState
        {
            IsOpen = isOpen,
            State = isOpen ? "Open" : "Closed",
            LastUpdated = state?.TimestampUtc ?? DateTime.MinValue,
            Contact = contacts.Contacts.FirstOrDefault(c => c.GroupName == groupName),
        };
    }

    /// <summary>Starts opening every shutter that is not already open; completion is reported later in the chat.</summary>
    [McpServerTool(ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Starts opening every shutter and blind that is not already fully open and returns immediately; shutters take up to a minute to travel and completion is posted to the chat later.")]
    public async Task<ShutterMovementStarted> OpenAllHouseShutters(CancellationToken cancellationToken = default)
    {
        var targets = await knxQuerySvc.OpenAllShutters(cancellationToken);
        return Track($"Opening {targets.Length} shutters", targets, $"Opening started for {targets.Length} shutters");
    }

    /// <summary>Starts closing every shutter and blind slat; completion is reported later in the chat.</summary>
    [McpServerTool(ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Starts closing every shutter and blind that is not already fully closed, then closes blind slats for privacy, and returns immediately; shutters take up to a minute to travel and completion is posted to the chat later.")]
    public async Task<ShutterMovementStarted> CloseAllHouseShutters(CancellationToken cancellationToken = default)
    {
        var targets = await knxQuerySvc.CloseAllShutters(cancellationToken);
        return Track($"Closing {targets.Length} shutters", targets, $"Closing started for {targets.Length} shutters");
    }

    private ShutterMovementStarted Track(string description, KnxShutterTarget[] targets, string started)
    {
        if (targets.Length == 0)
            return new ShutterMovementStarted { Shutters = [], Message = "Nothing to do: every shutter is already at its target." };

        var config = knxConfig.Value;
        var movement = new KnxShutterMovement(
            targets,
            config.ShutterPositionTolerancePct,
            ct => knxQuerySvc.ListShutters(cancellationToken: ct),
            knxQuerySvc.MoveShutter);
        var operationId = trackedOperations.Start(new ShutterMovementOperation(
            description,
            movement,
            TimeSpan.FromMilliseconds(config.ShutterMovementTimeoutMs)));
        return new ShutterMovementStarted
        {
            OperationId = operationId,
            Shutters = targets,
            Message = $"{started}. They have not finished moving yet; progress and completion will be posted to the chat.",
        };
    }

    /// <inheritdoc cref="IKnxQueryService.FilterGroupAddresses"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
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
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists the floors (storeys) of the house from top to bottom.")]
    public Task<List<KnxRoom>> GetHouseFloors(CancellationToken cancellationToken = default)
        => knxQuerySvc.ListFloors(cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.ListRooms"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists every room in the house grouped by floor.")]
    public Task<List<KnxRoom>> GetHouseRooms(CancellationToken cancellationToken = default)
        => knxQuerySvc.ListRooms(cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.ListShutters"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns the current open/closed status and position of all shutters and blinds, optionally filtered by room. Position: 0=fully open, 100=fully closed (KNX convention).")]
    public Task<List<KnxShutter>> GetHouseShutterStates(
        [Description("Room filter (e.g. Kitchen, Office, LivingRoom).")]
        string? room = null,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.ListShutters(room, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.GetShutter"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns the current status of a single shutter — open/closed position, slats, direction and diagnostics. Position: 0=fully open, 100=fully closed (KNX convention).")]
    public Task<KnxShutter?> GetHouseShutterState(
        [Description("e.g. OG-BL-FamilyBathroom-West.")]
        string groupName,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.GetShutter(groupName, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.GetContactSummary"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Gets counts and states for physical door and window contacts. States are open, closed, or unknown; for shutter or blind positions use GetHouseShutterStates instead.")]
    public Task<KnxContactSummary> GetHouseContactStates(
        [Description("Optional room filter (e.g. Kitchen, Office, LivingRoom).")]
        string? room = null,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.GetContactSummary(room, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.GetDoorLockSummary"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Gets counts and states for door locks. States are locked, unlocked, or unknown; false means locked and true means unlocked for these sensors.")]
    public Task<KnxDoorLockSummary> GetHouseDoorLockStates(
        [Description("Optional room filter (e.g. Entrance or GuestRoom).")]
        string? room = null,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.GetDoorLockSummary(room, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.ListPowerOutlets"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists all power outlets with current on/off state, optionally filtered by room.")]
    public Task<List<KnxPowerOutlet>> GetHousePowerOutlets(
        [Description("Room filter (e.g. Kitchen, Office, LivingRoom).")]
        string? room = null,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.ListPowerOutlets(room, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.GetPowerOutlet"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Gets one power outlet by group name — on/off state.")]
    public Task<KnxPowerOutlet?> GetHousePowerOutlet(
        [Description("e.g. DG-SD-Office.")]
        string groupName,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.GetPowerOutlet(groupName, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.ValidateGroupName"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Checks whether a KNX group name exists and returns its child functions.")]
    public Task<KnxGroupAddressGroup?> ValidateGroupName(
        [Description("e.g. OG-BL-FamilyBathroom-West or EG-LI-Entrance-DL.")]
        string groupName,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.ValidateGroupName(groupName, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.ValidateGroupAddress"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Checks whether a KNX group address exists by name or numeric address.")]
    public Task<KnxGroupAddressParsed?> ValidateGroupAddress(
        [Description("Name (e.g. EG-LI-Entrance-DL-SW) or numeric (e.g. 1/2/3).")]
        string groupAddress,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.ValidateGroupAddress(groupAddress, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.LetsTestXmlComments"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Connectivity test — returns 1337 on success.")]
    public int TestBusConnectivity() => knxQuerySvc.LetsTestXmlComments();

    /// <inheritdoc cref="IKnxQueryService.ListHvacZones"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists all heating zones with setpoint, temperature and valve state, optionally filtered by room.")]
    public Task<List<KnxHvacZone>> GetHouseHeatingZones(
        [Description("Room filter (e.g. Kitchen, Office, LivingRoom).")]
        string? room = null,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.ListHvacZones(room, cancellationToken);

    /// <inheritdoc cref="IKnxQueryService.GetHvacZone"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Gets one heating zone by group name — setpoint, temperature and valve state.")]
    public Task<KnxHvacZone?> GetHouseHeatingZone(
        [Description("e.g. EG-HZ-Kitchen.")]
        string groupName,
        CancellationToken cancellationToken = default)
        => knxQuerySvc.GetHvacZone(groupName, cancellationToken);
}

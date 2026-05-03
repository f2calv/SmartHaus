namespace CasCap.Services;

/// <summary>
/// MCP wrapper that exposes all lighting operations as MCP tools — KNX ceiling/wall lights
/// (via <see cref="IKnxQueryService"/>), smart bulbs (via <see cref="IWizQueryService"/>)
/// and smart-plug-controlled lights (via <see cref="IShellyQueryService"/>).
/// </summary>
/// <remarks>
/// All three dependencies are nullable because this service is registered when any of
/// Wiz, KNX, or Shelly is enabled, but the other features may not be present in the
/// current deployment.
/// </remarks>
[McpServerToolType]
public partial class SmartLightingMcpQueryService(
    IKnxQueryService? knxQuerySvc = null,
    IWizQueryService? wizQuerySvc = null,
    IShellyQueryService? shellyQuerySvc = null)
{
    //TODO: I dont like these group addresses hardcoded here, need to change this
    const string DoorLightGroupName = "EG-LI-Entrance(FrontDoor)-Outdoor-DL";
    const string OfficeLightGroupName = "DG-LI-Office-DL-South";
    const string DeskLampDeviceName = "DG-SD-Office(DeskLamp)";

    /// <inheritdoc cref="IKnxQueryService.SetLightState"/>
    [McpServerTool]
    [Description("Changes a single light — switch on/off, dim or set colour. Requires KNX feature.")]
    public Task<KnxStateChangeResponse> ChangeHouseLightState(
        [Description("GroupName (LI category) plus exactly one of: IsOn, DimValue (0–100) or HexColour (6-char hex).")]
        KnxLightStateChangeRequest request,
        [Description("When true, validates without sending commands.")]
        bool dryRun = false,
        CancellationToken cancellationToken = default)
        => knxQuerySvc is not null
            ? knxQuerySvc.SetLightState(request, dryRun, cancellationToken)
            : throw new InvalidOperationException("KNX feature is not enabled.");

    /// <inheritdoc cref="IKnxQueryService.TurnAllLightsOff"/>
    [McpServerTool]
    [Description("Turns off every light that is currently on in the house. Requires KNX feature.")]
    [RequiresApproval(Reason = "Bulk state change — turns off all lights in the house.")]
    public Task<string[]> SwitchOffAllHouseLights(CancellationToken cancellationToken = default)
        => knxQuerySvc is not null
            ? knxQuerySvc.TurnAllLightsOff(cancellationToken)
            : throw new InvalidOperationException("KNX feature is not enabled.");

    /// <summary>Turns on the exterior light at the front door.</summary>
    [McpServerTool]
    [Description("Turns on the exterior light at the front door.")]
    public Task<KnxStateChangeResponse> TurnOnHouseDoorLight(CancellationToken cancellationToken = default)
        => knxQuerySvc is not null
            ? knxQuerySvc.SetLightState(new KnxLightStateChangeRequest { GroupName = DoorLightGroupName, IsOn = true }, cancellationToken: cancellationToken)
            : throw new InvalidOperationException("KNX feature is not enabled.");

    /// <summary>Turns off the exterior light at the front door.</summary>
    [McpServerTool]
    [Description("Turns off the exterior light at the front door.")]
    public Task<KnxStateChangeResponse> TurnOffHouseDoorLight(CancellationToken cancellationToken = default)
        => knxQuerySvc is not null
            ? knxQuerySvc.SetLightState(new KnxLightStateChangeRequest { GroupName = DoorLightGroupName, IsOn = false }, cancellationToken: cancellationToken)
            : throw new InvalidOperationException("KNX feature is not enabled.");

    /// <summary>Turns on all lights in the DG Office.</summary>
    [McpServerTool]
    [Description("Turns on all lights in the top-floor (DG) office.")]
    public Task<KnxStateChangeResponse> TurnOnHouseOfficeLights(CancellationToken cancellationToken = default)
        => knxQuerySvc is not null
            ? knxQuerySvc.SetLightState(new KnxLightStateChangeRequest { GroupName = OfficeLightGroupName, IsOn = true }, cancellationToken: cancellationToken)
            : throw new InvalidOperationException("KNX feature is not enabled.");

    /// <summary>Turns off all lights in the DG Office.</summary>
    [McpServerTool]
    [Description("Turns off all lights in the top-floor (DG) office.")]
    public Task<KnxStateChangeResponse> TurnOffHouseOfficeLights(CancellationToken cancellationToken = default)
        => knxQuerySvc is not null
            ? knxQuerySvc.SetLightState(new KnxLightStateChangeRequest { GroupName = OfficeLightGroupName, IsOn = false }, cancellationToken: cancellationToken)
            : throw new InvalidOperationException("KNX feature is not enabled.");

    /// <inheritdoc cref="IKnxQueryService.ListLights"/>
    [McpServerTool]
    [Description("Lists all lights with full detail — switch, dim, colour and lux, optionally filtered by room.")]
    public Task<List<KnxLight>> GetHouseSmartLights(
        [Description("Room filter (e.g. Kitchen, Office, LivingRoom).")]
        string? room = null,
        CancellationToken cancellationToken = default)
        => knxQuerySvc is not null
            ? knxQuerySvc.ListLights(room, cancellationToken)
            : throw new InvalidOperationException("KNX feature is not enabled.");

    /// <inheritdoc cref="IKnxQueryService.GetLight"/>
    [McpServerTool]
    [Description("Gets one light by group name — switch, dim, colour and lux.")]
    public Task<KnxLight?> GetHouseLightState(
        [Description("e.g. DG-LI-Office-DL-South.")]
        string groupName,
        CancellationToken cancellationToken = default)
        => knxQuerySvc is not null
            ? knxQuerySvc.GetLight(groupName, cancellationToken)
            : throw new InvalidOperationException("KNX feature is not enabled.");

    /// <inheritdoc cref="IKnxQueryService.GetAllLighting(string?, string?, CancellationToken)"/>
    [McpServerTool]
    [Description("Quick on/off overview of all lights, filterable by floor and orientation.")]
    public Task<Dictionary<string, State?>> GetHouseLightStates(
        [Description("Values: KG (basement), EG (ground), OG (upper), DG (top).")]
        string? floor = null,
        [Description("Values: North, East, South, West.")]
        string? orientation = null,
        CancellationToken cancellationToken = default)
        => knxQuerySvc is not null
            ? knxQuerySvc.GetAllLighting(floor, orientation, cancellationToken)
            : throw new InvalidOperationException("KNX feature is not enabled.");

    /// <inheritdoc cref="IWizQueryService.GetDiscoveredBulbs"/>
    [McpServerTool]
    [Description("Lists all smart bulbs currently discovered on the local network with their state and configuration. Requires Wiz feature.")]
    public IReadOnlyDictionary<string, WizBulb> GetSmartLights()
        => wizQuerySvc is not null
            ? wizQuerySvc.GetDiscoveredBulbs()
            : throw new InvalidOperationException("Wiz feature is not enabled.");

    /// <inheritdoc cref="IWizQueryService.GetPilot"/>
    [McpServerTool]
    [Description("Current state of one smart bulb — on/off, brightness, colour, colour temperature and active scene.")]
    public Task<WizPilotState?> GetSmartLightState(
        [Description("Device name, MAC address, or IP address of the target bulb.")]
        string bulbIdentifier,
        CancellationToken cancellationToken = default)
        => wizQuerySvc is not null
            ? wizQuerySvc.GetPilot(bulbIdentifier, cancellationToken)
            : throw new InvalidOperationException("Wiz feature is not enabled.");

    /// <inheritdoc cref="IWizQueryService.SetPilot"/>
    [McpServerTool]
    [Description("Adjusts brightness, colour, temperature or scene of one smart bulb.")]
    public Task<bool> AdjustSmartLightOutput(
        [Description("Device name, MAC address, or IP address of the target bulb.")]
        string bulbIdentifier,
        [Description("Desired state: include dimming (10–100), temp (2200–6500 K), r/g/b (0–255), sceneId, or any combination.")]
        WizSetPilotRequest request,
        CancellationToken cancellationToken = default)
        => wizQuerySvc is not null
            ? wizQuerySvc.SetPilot(bulbIdentifier, request, cancellationToken)
            : throw new InvalidOperationException("Wiz feature is not enabled.");

    /// <inheritdoc cref="IWizQueryService.SetPowerState"/>
    [McpServerTool]
    [Description("Turns a single smart bulb on or off.")]
    public Task<bool> SetSmartLightPower(
        [Description("Device name, MAC address, or IP address of the target bulb.")]
        string bulbIdentifier,
        [Description("True to turn on, false to turn off.")]
        bool on,
        CancellationToken cancellationToken = default)
        => wizQuerySvc is not null
            ? wizQuerySvc.SetPowerState(bulbIdentifier, on, cancellationToken)
            : throw new InvalidOperationException("Wiz feature is not enabled.");

    /// <summary>Turns on the desk lamp in the top-floor (DG) office via Shelly smart plug.</summary>
    [McpServerTool]
    [Description("Turns on the desk lamp in the top-floor (DG) office.")]
    public Task<ShellyRelayControlResponse?> TurnOnOfficeDeskLamp()
        => shellyQuerySvc is not null
            ? shellyQuerySvc.SetRelayState(DeskLampDeviceName, turnOn: true)
            : throw new InvalidOperationException("Shelly feature is not enabled.");

    /// <summary>Turns off the desk lamp in the top-floor (DG) office via Shelly smart plug.</summary>
    [McpServerTool]
    [Description("Turns off the desk lamp in the top-floor (DG) office.")]
    public Task<ShellyRelayControlResponse?> TurnOffOfficeDeskLamp()
        => shellyQuerySvc is not null
            ? shellyQuerySvc.SetRelayState(DeskLampDeviceName, turnOn: false)
            : throw new InvalidOperationException("Shelly feature is not enabled.");
}

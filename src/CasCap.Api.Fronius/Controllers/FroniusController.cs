namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Fronius inverter data queries.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class FroniusController(IFroniusQueryService froniusQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="FroniusQueryService.GetInverterSnapshot"/>
    [HttpGet]
    [ProducesResponseType<InverterSnapshot>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInverterSnapshot()
        => Ok(await froniusQuerySvc.GetInverterSnapshot());

    /// <inheritdoc cref="FroniusQueryService.GetInverterReadings"/>
    [HttpGet("readings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetInverterReadings(int limit = 100)
        => Ok(froniusQuerySvc.GetInverterReadings(limit));

    /// <inheritdoc cref="FroniusQueryService.GetPowerFlowRealtimeData"/>
    [HttpGet("powerflow")]
    [ProducesResponseType<PowerFlowRealtimeData>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPowerFlowRealtimeData()
        => Ok(await froniusQuerySvc.GetPowerFlowRealtimeData());

    /// <inheritdoc cref="FroniusQueryService.GetInverterRealtimeData"/>
    [HttpGet("inverter/realtimedata")]
    [ProducesResponseType<CommonInverterData>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInverterRealtimeData([FromQuery] string dataCollection = "CommonInverterData")
        => Ok(await froniusQuerySvc.GetInverterRealtimeData(dataCollection));

    /// <inheritdoc cref="FroniusQueryService.GetInverterInfo"/>
    [HttpGet("inverter/info")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInverterInfo()
        => Ok(await froniusQuerySvc.GetInverterInfo());

    /// <inheritdoc cref="FroniusQueryService.GetActiveDeviceInfo"/>
    [HttpGet("devices")]
    [ProducesResponseType<ActiveDeviceInfoData>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveDeviceInfo()
        => Ok(await froniusQuerySvc.GetActiveDeviceInfo());

    /// <inheritdoc cref="FroniusQueryService.GetMeterRealtimeData"/>
    [HttpGet("meter")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMeterRealtimeData([FromQuery] string scope = "System", [FromQuery] int? deviceId = null)
        => Ok(await froniusQuerySvc.GetMeterRealtimeData(scope, deviceId));

    /// <inheritdoc cref="FroniusQueryService.GetStorageRealtimeData"/>
    [HttpGet("storage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStorageRealtimeData([FromQuery] string scope = "System", [FromQuery] int? deviceId = null)
        => Ok(await froniusQuerySvc.GetStorageRealtimeData(scope, deviceId));
}

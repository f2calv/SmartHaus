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
    public async Task<Ok<InverterSnapshot>> GetInverterSnapshot()
        => TypedResults.Ok(await froniusQuerySvc.GetInverterSnapshot());

    /// <inheritdoc cref="FroniusQueryService.GetInverterReadings"/>
    [HttpGet("readings")]
    public Ok<IAsyncEnumerable<FroniusEvent>> GetInverterReadings(int limit = 100)
        => TypedResults.Ok(froniusQuerySvc.GetInverterReadings(limit));

    /// <inheritdoc cref="FroniusQueryService.GetPowerFlowRealtimeData"/>
    [HttpGet("powerflow")]
    public async Task<Ok<PowerFlowRealtimeData>> GetPowerFlowRealtimeData()
        => TypedResults.Ok(await froniusQuerySvc.GetPowerFlowRealtimeData());

    /// <inheritdoc cref="FroniusQueryService.GetInverterRealtimeData"/>
    [HttpGet("inverter/realtimedata")]
    public async Task<Ok<CommonInverterData>> GetInverterRealtimeData([FromQuery] string dataCollection = "CommonInverterData")
        => TypedResults.Ok(await froniusQuerySvc.GetInverterRealtimeData(dataCollection));

    /// <inheritdoc cref="FroniusQueryService.GetInverterInfo"/>
    [HttpGet("inverter/info")]
    public async Task<Ok<Dictionary<string, InverterInfoEntry>>> GetInverterInfo()
        => TypedResults.Ok(await froniusQuerySvc.GetInverterInfo());

    /// <inheritdoc cref="FroniusQueryService.GetActiveDeviceInfo"/>
    [HttpGet("devices")]
    public async Task<Ok<ActiveDeviceInfoData>> GetActiveDeviceInfo()
        => TypedResults.Ok(await froniusQuerySvc.GetActiveDeviceInfo());

    /// <inheritdoc cref="FroniusQueryService.GetMeterRealtimeData"/>
    [HttpGet("meter")]
    public async Task<Ok<Dictionary<string, MeterRealtimeData>>> GetMeterRealtimeData([FromQuery] string scope = "System", [FromQuery] int? deviceId = null)
        => TypedResults.Ok(await froniusQuerySvc.GetMeterRealtimeData(scope, deviceId));

    /// <inheritdoc cref="FroniusQueryService.GetStorageRealtimeData"/>
    [HttpGet("storage")]
    public async Task<Ok<Dictionary<string, StorageRealtimeData>>> GetStorageRealtimeData([FromQuery] string scope = "System", [FromQuery] int? deviceId = null)
        => TypedResults.Ok(await froniusQuerySvc.GetStorageRealtimeData(scope, deviceId));
}

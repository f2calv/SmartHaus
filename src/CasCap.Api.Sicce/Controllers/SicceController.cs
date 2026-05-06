namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Sicce water pump data queries.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class SicceController(ISicceQueryService sicceQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="SicceQueryService.GetSnapshot"/>
    [HttpGet]
    public async Task<Ok<SicceSnapshot>> GetSnapshot()
        => TypedResults.Ok(await sicceQuerySvc.GetSnapshot());

    /// <inheritdoc cref="SicceQueryService.GetReadings"/>
    [HttpGet("readings")]
    public Ok<IAsyncEnumerable<SicceEvent>> GetReadings(int limit = 100)
        => TypedResults.Ok(sicceQuerySvc.GetReadings(limit));

    /// <inheritdoc cref="SicceQueryService.GetDeviceInfo"/>
    [HttpGet("device")]
    public async Task<Ok<DeviceInfo>> GetDeviceInfo()
        => TypedResults.Ok(await sicceQuerySvc.GetDeviceInfo());
}

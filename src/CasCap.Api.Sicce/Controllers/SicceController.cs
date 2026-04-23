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
    [ProducesResponseType<SicceSnapshot>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSnapshot()
        => Ok(await sicceQuerySvc.GetSnapshot());

    /// <inheritdoc cref="SicceQueryService.GetReadings"/>
    [HttpGet("readings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetReadings(int limit = 100)
        => Ok(sicceQuerySvc.GetReadings(limit));

    /// <inheritdoc cref="SicceQueryService.GetDeviceInfo"/>
    [HttpGet("device")]
    [ProducesResponseType<DeviceInfo>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeviceInfo()
        => Ok(await sicceQuerySvc.GetDeviceInfo());
}

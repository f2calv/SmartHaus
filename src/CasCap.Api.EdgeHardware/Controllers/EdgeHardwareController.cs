namespace CasCap.Controllers;

/// <summary>
/// REST API controller for edge hardware metrics queries.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class EdgeHardwareController(IEdgeHardwareQueryService edgeHardwareQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="EdgeHardwareQueryService.GetLatestSnapshots"/>
    [HttpGet]
    [ProducesResponseType<List<EdgeHardwareSnapshot>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLatestSnapshots()
        => Ok(await edgeHardwareQuerySvc.GetLatestSnapshots());

    /// <inheritdoc cref="EdgeHardwareQueryService.GetEvents"/>
    [HttpGet("events")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetEvents(int limit = 100)
        => Ok(edgeHardwareQuerySvc.GetEvents(limit));

}

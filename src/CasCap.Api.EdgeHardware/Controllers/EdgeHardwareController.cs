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
    public async Task<Ok<List<EdgeHardwareSnapshot>>> GetLatestSnapshots()
        => TypedResults.Ok(await edgeHardwareQuerySvc.GetLatestSnapshots());

    /// <inheritdoc cref="EdgeHardwareQueryService.GetEvents"/>
    [HttpGet("events")]
    public Ok<IAsyncEnumerable<EdgeHardwareEvent>> GetEvents(int limit = 100)
        => TypedResults.Ok(edgeHardwareQuerySvc.GetEvents(limit));

}

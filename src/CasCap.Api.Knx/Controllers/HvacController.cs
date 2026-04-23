namespace CasCap.Controllers;

/// <summary>
/// REST API controller for KNX HVAC queries and commands.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class HvacController(IKnxQueryService knxQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="KnxQueryService.ListHvacZones"/>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListHvacZones([FromQuery] string? room = null, CancellationToken cancellationToken = default)
        => Ok(await knxQuerySvc.ListHvacZones(room, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.GetHvacZone"/>
    [HttpGet("{groupName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHvacZone(string groupName, CancellationToken cancellationToken = default)
    {
        var result = await knxQuerySvc.GetHvacZone(groupName, cancellationToken);
        return result is not null ? Ok(result) : NotFound();
    }

    /// <inheritdoc cref="KnxQueryService.SetHvacState"/>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> SetHvacState([FromBody] KnxHvacZoneStateChangeRequest request, [FromQuery] bool dryRun = false, CancellationToken cancellationToken = default)
        => Accepted(await knxQuerySvc.SetHvacState(request, dryRun, cancellationToken));
}

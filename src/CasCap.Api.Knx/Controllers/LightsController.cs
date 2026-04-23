namespace CasCap.Controllers;

/// <summary>
/// REST API controller for KNX lighting queries and commands.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class LightsController(IKnxQueryService knxQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="KnxQueryService.ListLights"/>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListLights([FromQuery] string? room = null, CancellationToken cancellationToken = default)
        => Ok(await knxQuerySvc.ListLights(room, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.GetLight"/>
    [HttpGet("{groupName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLight(string groupName, CancellationToken cancellationToken = default)
    {
        var result = await knxQuerySvc.GetLight(groupName, cancellationToken);
        return result is not null ? Ok(result) : NotFound();
    }

    /// <inheritdoc cref="KnxQueryService.SetLightState"/>
    [HttpPost("state")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> SetLightState([FromBody] KnxLightStateChangeRequest request, [FromQuery] bool dryRun = false, CancellationToken cancellationToken = default)
        => Accepted(await knxQuerySvc.SetLightState(request, dryRun, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.TurnAllLightsOff"/>
    [HttpPost("state/all-off")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> TurnAllLightsOff(CancellationToken cancellationToken = default)
        => Accepted(await knxQuerySvc.TurnAllLightsOff(cancellationToken));

}

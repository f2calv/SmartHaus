namespace CasCap.Controllers;

/// <summary>
/// REST API controller for KNX shutter/blind queries and commands.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class ShuttersController(IKnxQueryService knxQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="KnxQueryService.ListShutters"/>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListShutters([FromQuery] string? room = null, CancellationToken cancellationToken = default)
        => Ok(await knxQuerySvc.ListShutters(room, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.GetShutter"/>
    [HttpGet("{groupName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetShutter(string groupName, CancellationToken cancellationToken = default)
    {
        var result = await knxQuerySvc.GetShutter(groupName, cancellationToken);
        return result is not null ? Ok(result) : NotFound();
    }

    /// <inheritdoc cref="KnxQueryService.SetShutterState"/>
    [HttpPost("state")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> SetShutterState([FromBody] KnxShutterStateChangeRequest request, [FromQuery] bool dryRun = false, CancellationToken cancellationToken = default)
        => Accepted(await knxQuerySvc.SetShutterState(request, dryRun, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.CloseAllShutters"/>
    [HttpPost("state/all-close")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> CloseAllShutters(CancellationToken cancellationToken = default)
        => Accepted(await knxQuerySvc.CloseAllShutters(cancellationToken));

    /// <inheritdoc cref="KnxQueryService.OpenAllShutters"/>
    [HttpPost("state/all-open")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> OpenAllShutters(CancellationToken cancellationToken = default)
        => Accepted(await knxQuerySvc.OpenAllShutters(cancellationToken));
}

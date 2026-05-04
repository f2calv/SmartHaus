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
    public async Task<Ok<List<KnxShutter>>> ListShutters([FromQuery] string? room = null, CancellationToken cancellationToken = default)
        => TypedResults.Ok(await knxQuerySvc.ListShutters(room, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.GetShutter"/>
    [HttpGet("{groupName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<Results<Ok<KnxShutter>, NotFound>> GetShutter(string groupName, CancellationToken cancellationToken = default)
    {
        var result = await knxQuerySvc.GetShutter(groupName, cancellationToken);
        return result is not null ? TypedResults.Ok(result) : TypedResults.NotFound();
    }

    /// <inheritdoc cref="KnxQueryService.SetShutterState"/>
    [HttpPost("state")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<Accepted<KnxStateChangeResponse>> SetShutterState([FromBody] KnxShutterStateChangeRequest request, [FromQuery] bool dryRun = false, CancellationToken cancellationToken = default)
        => TypedResults.Accepted((string?)null, await knxQuerySvc.SetShutterState(request, dryRun, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.CloseAllShutters"/>
    [HttpPost("state/all-close")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<Accepted<string[]>> CloseAllShutters(CancellationToken cancellationToken = default)
        => TypedResults.Accepted((string?)null, await knxQuerySvc.CloseAllShutters(cancellationToken));

    /// <inheritdoc cref="KnxQueryService.OpenAllShutters"/>
    [HttpPost("state/all-open")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<Accepted<string[]>> OpenAllShutters(CancellationToken cancellationToken = default)
        => TypedResults.Accepted((string?)null, await knxQuerySvc.OpenAllShutters(cancellationToken));
}

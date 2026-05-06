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
    public async Task<Ok<List<KnxShutter>>> ListShutters([FromQuery] string? room = null, CancellationToken cancellationToken = default)
        => TypedResults.Ok(await knxQuerySvc.ListShutters(room, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.GetShutter"/>
    [HttpGet("{groupName}")]
    public async Task<Results<Ok<KnxShutter>, NotFound>> GetShutter(string groupName, CancellationToken cancellationToken = default)
    {
        var result = await knxQuerySvc.GetShutter(groupName, cancellationToken);
        return result is not null ? TypedResults.Ok(result) : TypedResults.NotFound();
    }

    /// <inheritdoc cref="KnxQueryService.SetShutterState"/>
    [HttpPost("state")]
    public async Task<Accepted<KnxStateChangeResponse>> SetShutterState([FromBody] KnxShutterStateChangeRequest request, [FromQuery] bool dryRun = false, CancellationToken cancellationToken = default)
        => TypedResults.Accepted((string?)null, await knxQuerySvc.SetShutterState(request, dryRun, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.CloseAllShutters"/>
    [HttpPost("state/all-close")]
    public async Task<Accepted<string[]>> CloseAllShutters(CancellationToken cancellationToken = default)
        => TypedResults.Accepted((string?)null, await knxQuerySvc.CloseAllShutters(cancellationToken));

    /// <inheritdoc cref="KnxQueryService.OpenAllShutters"/>
    [HttpPost("state/all-open")]
    public async Task<Accepted<string[]>> OpenAllShutters(CancellationToken cancellationToken = default)
        => TypedResults.Accepted((string?)null, await knxQuerySvc.OpenAllShutters(cancellationToken));
}

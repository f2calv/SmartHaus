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
    public async Task<Ok<List<KnxLight>>> ListLights([FromQuery] string? room = null, CancellationToken cancellationToken = default)
        => TypedResults.Ok(await knxQuerySvc.ListLights(room, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.GetLight"/>
    [HttpGet("{groupName}")]
    public async Task<Results<Ok<KnxLight>, NotFound>> GetLight(string groupName, CancellationToken cancellationToken = default)
    {
        var result = await knxQuerySvc.GetLight(groupName, cancellationToken);
        return result is not null ? TypedResults.Ok(result) : TypedResults.NotFound();
    }

    /// <inheritdoc cref="KnxQueryService.SetLightState"/>
    [HttpPost("state")]
    public async Task<Accepted<KnxStateChangeResponse>> SetLightState([FromBody] KnxLightStateChangeRequest request, [FromQuery] bool dryRun = false, CancellationToken cancellationToken = default)
        => TypedResults.Accepted((string?)null, await knxQuerySvc.SetLightState(request, dryRun, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.TurnAllLightsOff"/>
    [HttpPost("state/all-off")]
    public async Task<Accepted<string[]>> TurnAllLightsOff(CancellationToken cancellationToken = default)
        => TypedResults.Accepted((string?)null, await knxQuerySvc.TurnAllLightsOff(cancellationToken));

}

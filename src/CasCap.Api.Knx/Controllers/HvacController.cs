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
    public async Task<Ok<List<KnxHvacZone>>> ListHvacZones([FromQuery] string? room = null, CancellationToken cancellationToken = default)
        => TypedResults.Ok(await knxQuerySvc.ListHvacZones(room, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.GetHvacZone"/>
    [HttpGet("{groupName}")]
    public async Task<Results<Ok<KnxHvacZone>, NotFound>> GetHvacZone(string groupName, CancellationToken cancellationToken = default)
    {
        var result = await knxQuerySvc.GetHvacZone(groupName, cancellationToken);
        return result is not null ? TypedResults.Ok(result) : TypedResults.NotFound();
    }

    /// <inheritdoc cref="KnxQueryService.SetHvacState"/>
    [HttpPost]
    public async Task<Accepted<KnxStateChangeResponse>> SetHvacState([FromBody] KnxHvacZoneStateChangeRequest request, [FromQuery] bool dryRun = false, CancellationToken cancellationToken = default)
        => TypedResults.Accepted((string?)null, await knxQuerySvc.SetHvacState(request, dryRun, cancellationToken));
}

namespace CasCap.Controllers;

/// <summary>
/// REST API controller for KNX power outlet queries and commands.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class PowerOutletsController(IKnxQueryService knxQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="KnxQueryService.ListPowerOutlets"/>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<Ok<List<KnxPowerOutlet>>> ListPowerOutlets([FromQuery] string? room = null, CancellationToken cancellationToken = default)
        => TypedResults.Ok(await knxQuerySvc.ListPowerOutlets(room, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.GetPowerOutlet"/>
    [HttpGet("{groupName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<Results<Ok<KnxPowerOutlet>, NotFound>> GetPowerOutlet(string groupName, CancellationToken cancellationToken = default)
    {
        var result = await knxQuerySvc.GetPowerOutlet(groupName, cancellationToken);
        return result is not null ? TypedResults.Ok(result) : TypedResults.NotFound();
    }

    /// <inheritdoc cref="KnxQueryService.SetPowerOutletState"/>
    [HttpPost("state")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<Accepted<KnxStateChangeResponse>> SetPowerOutletState([FromBody] KnxPowerOutletStateChangeRequest request, [FromQuery] bool dryRun = false, CancellationToken cancellationToken = default)
        => TypedResults.Accepted((string?)null, await knxQuerySvc.SetPowerOutletState(request, dryRun, cancellationToken));
}

namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Wiz smart lighting queries and actions.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class WizController(IWizQueryService wizQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="WizQueryService.GetDiscoveredBulbs"/>
    [HttpGet("bulbs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Ok<IReadOnlyDictionary<string, WizBulb>> GetBulbs()
        => TypedResults.Ok(wizQuerySvc.GetDiscoveredBulbs());

    /// <inheritdoc cref="WizQueryService.DiscoverBulbs"/>
    [HttpPost("bulbs/discover")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<Ok<IReadOnlyDictionary<string, WizBulb>>> DiscoverBulbs(CancellationToken cancellationToken)
        => TypedResults.Ok(await wizQuerySvc.DiscoverBulbs(cancellationToken));

    /// <inheritdoc cref="WizQueryService.GetPilot"/>
    [HttpGet("bulbs/{bulbIdentifier}/pilot")]
    [ProducesResponseType<WizPilotState>(StatusCodes.Status200OK)]
    public async Task<Ok<WizPilotState>> GetPilot(string bulbIdentifier, CancellationToken cancellationToken)
        => TypedResults.Ok(await wizQuerySvc.GetPilot(bulbIdentifier, cancellationToken));

    /// <inheritdoc cref="WizQueryService.SetPilot"/>
    [HttpPut("bulbs/{bulbIdentifier}/pilot")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<Ok<bool>> SetPilot(string bulbIdentifier, [FromBody] WizSetPilotRequest request, CancellationToken cancellationToken)
        => TypedResults.Ok(await wizQuerySvc.SetPilot(bulbIdentifier, request, cancellationToken));

    /// <inheritdoc cref="WizQueryService.SetPowerState"/>
    [HttpPut("bulbs/{bulbIdentifier}/power")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<Ok<bool>> SetPowerState(string bulbIdentifier, [FromQuery] bool on, CancellationToken cancellationToken)
        => TypedResults.Ok(await wizQuerySvc.SetPowerState(bulbIdentifier, on, cancellationToken));
}

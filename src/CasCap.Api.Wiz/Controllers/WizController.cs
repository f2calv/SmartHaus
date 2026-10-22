using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Wiz smart lighting queries and actions.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class WizController(IWizQueryService wizQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="WizQueryService.GetDiscoveredBulbs"/>
    [HttpGet("bulbs")]
    public Ok<IReadOnlyDictionary<string, WizBulb>> GetBulbs()
        => TypedResults.Ok(wizQuerySvc.GetDiscoveredBulbs());

    /// <inheritdoc cref="WizQueryService.DiscoverBulbs"/>
    [HttpPost("bulbs/discover")]
    public async Task<Ok<IReadOnlyDictionary<string, WizBulb>>> DiscoverBulbs(CancellationToken cancellationToken)
        => TypedResults.Ok(await wizQuerySvc.DiscoverBulbs(cancellationToken));

    /// <inheritdoc cref="WizQueryService.GetPilot"/>
    [HttpGet("bulbs/{bulbIdentifier}/pilot")]
    public async Task<Ok<WizPilotState>> GetPilot(string bulbIdentifier, CancellationToken cancellationToken)
        => TypedResults.Ok(await wizQuerySvc.GetPilot(bulbIdentifier, cancellationToken));

    /// <inheritdoc cref="WizQueryService.SetPilot"/>
    [HttpPut("bulbs/{bulbIdentifier}/pilot")]
    public async Task<Ok<bool>> SetPilot(string bulbIdentifier, [FromBody] WizSetPilotRequest request, CancellationToken cancellationToken)
        => TypedResults.Ok(await wizQuerySvc.SetPilot(bulbIdentifier, request, cancellationToken));

    /// <inheritdoc cref="WizQueryService.SetPowerState"/>
    [HttpPut("bulbs/{bulbIdentifier}/power")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<Ok<bool>> SetPowerState(string bulbIdentifier, [FromQuery, BindRequired] bool on, CancellationToken cancellationToken)
        => TypedResults.Ok(await wizQuerySvc.SetPowerState(bulbIdentifier, on, cancellationToken));
}

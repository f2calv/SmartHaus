namespace CasCap.Controllers;

/// <summary>REST API controller for KNX door-lock queries.</summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public sealed class DoorLocksController(IKnxQueryService knxQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="KnxQueryService.GetDoorLockSummary"/>
    [HttpGet]
    public async Task<Ok<KnxDoorLockSummary>> GetDoorLockSummary([FromQuery] string? room = null, CancellationToken cancellationToken = default)
        => TypedResults.Ok(await knxQuerySvc.GetDoorLockSummary(room, cancellationToken));
}

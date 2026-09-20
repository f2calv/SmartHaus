namespace CasCap.Controllers;

/// <summary>REST API controller for KNX door and window contact queries.</summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public sealed class ContactsController(IKnxQueryService knxQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="KnxQueryService.GetContactSummary"/>
    [HttpGet]
    public async Task<Ok<KnxContactSummary>> GetContactSummary([FromQuery] string? room = null, CancellationToken cancellationToken = default)
        => TypedResults.Ok(await knxQuerySvc.GetContactSummary(room, cancellationToken));
}
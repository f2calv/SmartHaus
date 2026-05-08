namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Dynamic DNS service queries.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class DDnsController(IDDnsQueryService dDnsQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="DDnsQueryService.GetCurrentIp"/>
    [HttpGet("ip")]
    public async Task<Ok<string>> GetCurrentIp(CancellationToken cancellationToken)
        => TypedResults.Ok((await dDnsQuerySvc.GetCurrentIp(cancellationToken))?.ToString());
}

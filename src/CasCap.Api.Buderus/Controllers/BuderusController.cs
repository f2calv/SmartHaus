namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Buderus KM200 heat pump data queries and control.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class BuderusController(IBuderusQueryService buderusQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="BuderusQueryService.GetSnapshot"/>
    [HttpGet]
    public async Task<Ok<BuderusSnapshot>> GetSnapshot()
        => TypedResults.Ok(await buderusQuerySvc.GetSnapshot());

    /// <inheritdoc cref="BuderusQueryService.GetEvents"/>
    [HttpGet("values")]
    public Ok<IAsyncEnumerable<BuderusEvent>> Get(CancellationToken cancellationToken)
        => TypedResults.Ok(buderusQuerySvc.GetEvents(cancellationToken: cancellationToken));

    /// <inheritdoc cref="BuderusQueryService.GetEvents"/>
    [HttpGet("{id}")]
    public Ok<IAsyncEnumerable<BuderusEvent>> GetById(string id, CancellationToken cancellationToken)
        => TypedResults.Ok(buderusQuerySvc.GetEvents(id, cancellationToken: cancellationToken));

    /// <inheritdoc cref="BuderusQueryService.SetDataPoint"/>
    [HttpPut("{*id}")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<Results<Ok, BadRequest<string>>> SetDataPoint(string id, [FromBody] SetDataPointRequest request, CancellationToken cancellationToken)
    {
        var result = await buderusQuerySvc.SetDataPoint(id, request.Value, cancellationToken);
        return result.Error is { } error ? TypedResults.BadRequest(error) : TypedResults.Ok();
    }
}

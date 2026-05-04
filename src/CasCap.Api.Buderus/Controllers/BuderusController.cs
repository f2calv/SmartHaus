namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Buderus KM200 heat pump data queries and control.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class BuderusController(IBuderusQueryService buderusQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="BuderusQueryService.GetSnapshot"/>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<Ok<BuderusSnapshot>> GetSnapshot()
        => TypedResults.Ok(await buderusQuerySvc.GetSnapshot());

    /// <inheritdoc cref="BuderusQueryService.GetEvents"/>
    [HttpGet("values")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Ok<IAsyncEnumerable<BuderusEvent>> Get()
        => TypedResults.Ok(buderusQuerySvc.GetEvents());

    /// <inheritdoc cref="BuderusQueryService.GetEvents"/>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Ok<IAsyncEnumerable<BuderusEvent>> GetById(string id)
        => TypedResults.Ok(buderusQuerySvc.GetEvents(id));

    /// <inheritdoc cref="BuderusQueryService.SetDataPoint"/>
    [HttpPut("{*id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<Results<Ok, BadRequest<string>>> SetDataPoint(string id, [FromBody] SetDataPointRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
            return TypedResults.BadRequest("Datapoint id must not be empty.");
        // Ensure the id starts with '/' so it matches the KM200 path convention
        var datapointId = id.StartsWith('/') ? id : $"/{id}";
        var success = await buderusQuerySvc.SetDataPoint(datapointId, request.Value, cancellationToken);
        return success ? TypedResults.Ok() : TypedResults.BadRequest($"Failed to write datapoint '{datapointId}'. The datapoint may not exist or may not be writeable.");
    }
}

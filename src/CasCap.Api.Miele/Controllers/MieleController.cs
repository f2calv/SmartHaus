namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Miele appliance queries and actions.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
public sealed class MieleController(IMieleQueryService mieleQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="MieleQueryService.GetDevices"/>
    [HttpGet("devices")]
    public async Task<Results<Ok<Dictionary<string, MieleDevice>>, NotFound>> GetDevices(
        [FromQuery] string language = "en", CancellationToken cancellationToken = default)
        => await mieleQuerySvc.GetDevices(language, cancellationToken) is { } devices
            ? TypedResults.Ok(devices) : TypedResults.NotFound();

    /// <inheritdoc cref="MieleQueryService.GetShortDevices"/>
    [HttpGet("devices/short")]
    public async Task<Results<Ok<MieleShortDevice[]>, NotFound>> GetShortDevices(
        [FromQuery] string language = "en", CancellationToken cancellationToken = default)
        => await mieleQuerySvc.GetShortDevices(language, cancellationToken) is { } devices
            ? TypedResults.Ok(devices) : TypedResults.NotFound();

    /// <inheritdoc cref="MieleQueryService.GetDevice"/>
    [HttpGet("devices/{deviceId}")]
    public async Task<Results<Ok<MieleDevice>, NotFound>> GetDevice(
        string deviceId, [FromQuery] string language = "en", CancellationToken cancellationToken = default)
        => await mieleQuerySvc.GetDevice(deviceId, language, cancellationToken) is { } device
            ? TypedResults.Ok(device) : TypedResults.NotFound();

    /// <inheritdoc cref="MieleQueryService.GetIdent"/>
    [HttpGet("devices/{deviceId}/ident")]
    public async Task<Results<Ok<MieleIdent>, NotFound>> GetIdent(
        string deviceId, [FromQuery] string language = "en", CancellationToken cancellationToken = default)
        => await mieleQuerySvc.GetIdent(deviceId, language, cancellationToken) is { } ident
            ? TypedResults.Ok(ident) : TypedResults.NotFound();

    /// <inheritdoc cref="MieleQueryService.GetState"/>
    [HttpGet("devices/{deviceId}/state")]
    public async Task<Results<Ok<MieleState>, NotFound>> GetState(
        string deviceId, [FromQuery] string language = "en", CancellationToken cancellationToken = default)
        => await mieleQuerySvc.GetState(deviceId, language, cancellationToken) is { } state
            ? TypedResults.Ok(state) : TypedResults.NotFound();

    /// <inheritdoc cref="MieleQueryService.GetActions"/>
    [HttpGet("devices/{deviceId}/actions")]
    public async Task<Results<Ok<MieleActions>, NotFound>> GetActions(string deviceId, CancellationToken cancellationToken = default)
        => await mieleQuerySvc.GetActions(deviceId, cancellationToken) is { } actions
            ? TypedResults.Ok(actions) : TypedResults.NotFound();

    /// <inheritdoc cref="MieleQueryService.PutAction"/>
    [HttpPut("devices/{deviceId}/actions")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<Results<Ok<bool>, BadRequest<string>, NotFound>> PutAction(
        string deviceId, [FromBody] MieleActionRequest action, CancellationToken cancellationToken = default)
        => await mieleQuerySvc.PutAction(deviceId, action, cancellationToken) switch
        {
            MieleCommandResult.Accepted => TypedResults.Ok(true),
            MieleCommandResult.Rejected => TypedResults.BadRequest("The appliance rejected the action."),
            MieleCommandResult.NotFound => TypedResults.NotFound(),
            _ => throw new System.Diagnostics.UnreachableException(),
        };

    /// <inheritdoc cref="MieleQueryService.GetPrograms"/>
    [HttpGet("devices/{deviceId}/programs")]
    public async Task<Results<Ok<MieleProgram[]>, NotFound>> GetPrograms(
        string deviceId, [FromQuery] string language = "en", CancellationToken cancellationToken = default)
        => await mieleQuerySvc.GetPrograms(deviceId, language, cancellationToken) is { } programs
            ? TypedResults.Ok(programs) : TypedResults.NotFound();

    /// <inheritdoc cref="MieleQueryService.PutProgram"/>
    [HttpPut("devices/{deviceId}/programs")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<Results<Ok<bool>, BadRequest<string>, NotFound>> PutProgram(
        string deviceId, [FromBody] MieleProgramRequest programRequest, CancellationToken cancellationToken = default)
        => await mieleQuerySvc.PutProgram(deviceId, programRequest, cancellationToken) switch
        {
            MieleCommandResult.Accepted => TypedResults.Ok(true),
            MieleCommandResult.Rejected => TypedResults.BadRequest("The appliance rejected the program."),
            MieleCommandResult.NotFound => TypedResults.NotFound(),
            _ => throw new System.Diagnostics.UnreachableException(),
        };
}

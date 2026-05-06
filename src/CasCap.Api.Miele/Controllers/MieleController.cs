namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Miele appliance queries and actions.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class MieleController(IMieleQueryService mieleQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="MieleQueryService.GetDevices"/>
    [HttpGet("devices")]
    public async Task<Ok<Dictionary<string, MieleDevice>>> GetDevices([FromQuery] string language = "en")
        => TypedResults.Ok(await mieleQuerySvc.GetDevices(language));

    /// <inheritdoc cref="MieleQueryService.GetShortDevices"/>
    [HttpGet("devices/short")]
    public async Task<Ok<MieleShortDevice[]>> GetShortDevices([FromQuery] string language = "en")
        => TypedResults.Ok(await mieleQuerySvc.GetShortDevices(language));

    /// <inheritdoc cref="MieleQueryService.GetDevice"/>
    [HttpGet("devices/{deviceId}")]
    public async Task<Ok<MieleDevice>> GetDevice(string deviceId, [FromQuery] string language = "en")
        => TypedResults.Ok(await mieleQuerySvc.GetDevice(deviceId, language));

    /// <inheritdoc cref="MieleQueryService.GetIdent"/>
    [HttpGet("devices/{deviceId}/ident")]
    public async Task<Ok<MieleIdent>> GetIdent(string deviceId, [FromQuery] string language = "en")
        => TypedResults.Ok(await mieleQuerySvc.GetIdent(deviceId, language));

    /// <inheritdoc cref="MieleQueryService.GetState"/>
    [HttpGet("devices/{deviceId}/state")]
    public async Task<Ok<MieleState>> GetState(string deviceId, [FromQuery] string language = "en")
        => TypedResults.Ok(await mieleQuerySvc.GetState(deviceId, language));

    /// <inheritdoc cref="MieleQueryService.GetActions"/>
    [HttpGet("devices/{deviceId}/actions")]
    public async Task<Ok<MieleActions>> GetActions(string deviceId)
        => TypedResults.Ok(await mieleQuerySvc.GetActions(deviceId));

    /// <inheritdoc cref="MieleQueryService.PutAction"/>
    [HttpPut("devices/{deviceId}/actions")]
    public async Task<Ok<bool>> PutAction(string deviceId, [FromBody] object action)
        => TypedResults.Ok(await mieleQuerySvc.PutAction(deviceId, action));

    /// <inheritdoc cref="MieleQueryService.GetPrograms"/>
    [HttpGet("devices/{deviceId}/programs")]
    public async Task<Ok<MieleProgram[]>> GetPrograms(string deviceId, [FromQuery] string language = "en")
        => TypedResults.Ok(await mieleQuerySvc.GetPrograms(deviceId, language));

    /// <inheritdoc cref="MieleQueryService.PutProgram"/>
    [HttpPut("devices/{deviceId}/programs")]
    public async Task<Ok<bool>> PutProgram(string deviceId, [FromBody] object programRequest)
        => TypedResults.Ok(await mieleQuerySvc.PutProgram(deviceId, programRequest));
}

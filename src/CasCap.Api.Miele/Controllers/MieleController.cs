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
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDevices([FromQuery] string language = "en")
        => Ok(await mieleQuerySvc.GetDevices(language));

    /// <inheritdoc cref="MieleQueryService.GetShortDevices"/>
    [HttpGet("devices/short")]
    [ProducesResponseType<MieleShortDevice[]>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShortDevices([FromQuery] string language = "en")
        => Ok(await mieleQuerySvc.GetShortDevices(language));

    /// <inheritdoc cref="MieleQueryService.GetDevice"/>
    [HttpGet("devices/{deviceId}")]
    [ProducesResponseType<MieleDevice>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDevice(string deviceId, [FromQuery] string language = "en")
        => Ok(await mieleQuerySvc.GetDevice(deviceId, language));

    /// <inheritdoc cref="MieleQueryService.GetIdent"/>
    [HttpGet("devices/{deviceId}/ident")]
    [ProducesResponseType<MieleIdent>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIdent(string deviceId, [FromQuery] string language = "en")
        => Ok(await mieleQuerySvc.GetIdent(deviceId, language));

    /// <inheritdoc cref="MieleQueryService.GetState"/>
    [HttpGet("devices/{deviceId}/state")]
    [ProducesResponseType<MieleState>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetState(string deviceId, [FromQuery] string language = "en")
        => Ok(await mieleQuerySvc.GetState(deviceId, language));

    /// <inheritdoc cref="MieleQueryService.GetActions"/>
    [HttpGet("devices/{deviceId}/actions")]
    [ProducesResponseType<MieleActions>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActions(string deviceId)
        => Ok(await mieleQuerySvc.GetActions(deviceId));

    /// <inheritdoc cref="MieleQueryService.PutAction"/>
    [HttpPut("devices/{deviceId}/actions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PutAction(string deviceId, [FromBody] object action)
        => Ok(await mieleQuerySvc.PutAction(deviceId, action));

    /// <inheritdoc cref="MieleQueryService.GetPrograms"/>
    [HttpGet("devices/{deviceId}/programs")]
    [ProducesResponseType<MieleProgram[]>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPrograms(string deviceId, [FromQuery] string language = "en")
        => Ok(await mieleQuerySvc.GetPrograms(deviceId, language));

    /// <inheritdoc cref="MieleQueryService.PutProgram"/>
    [HttpPut("devices/{deviceId}/programs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PutProgram(string deviceId, [FromBody] object programRequest)
        => Ok(await mieleQuerySvc.PutProgram(deviceId, programRequest));
}

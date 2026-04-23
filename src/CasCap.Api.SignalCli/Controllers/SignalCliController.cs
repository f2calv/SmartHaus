namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Signal CLI service queries.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class SignalCliController(SignalCliRestClientService signalCliRestClientSvc) : ControllerBase
{
    /// <inheritdoc cref="SignalCliRestClientService.GetAbout"/>
    [HttpGet("about")]
    [ProducesResponseType<SignalAbout>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAbout()
        => Ok(await signalCliRestClientSvc.GetAbout());

    /// <inheritdoc cref="SignalCliRestClientService.GetConfiguration"/>
    [HttpGet("configuration")]
    [ProducesResponseType<SignalConfiguration>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfiguration()
        => Ok(await signalCliRestClientSvc.GetConfiguration());

    /// <inheritdoc cref="SignalCliRestClientService.ListAccounts"/>
    [HttpGet("accounts")]
    [ProducesResponseType<string[]>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAccounts()
        => Ok(await signalCliRestClientSvc.ListAccounts());

    /// <inheritdoc cref="SignalCliRestClientService.ListContacts"/>
    [HttpGet("contacts")]
    [ProducesResponseType<SignalContact[]>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListContacts([FromQuery] string number)
        => Ok(await signalCliRestClientSvc.ListContacts(number));

    /// <inheritdoc cref="SignalCliRestClientService.ListGroups"/>
    [HttpGet("groups")]
    [ProducesResponseType<SignalGroup[]>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListGroups([FromQuery] string number)
        => Ok(await signalCliRestClientSvc.ListGroups(number));

    /// <inheritdoc cref="SignalCliRestClientService.ListLinkedDevices"/>
    [HttpGet("devices")]
    [ProducesResponseType<SignalDevice[]>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListLinkedDevices([FromQuery] string number)
        => Ok(await signalCliRestClientSvc.ListLinkedDevices(number));

    /// <inheritdoc cref="SignalCliRestClientService.ListIdentities"/>
    [HttpGet("identities")]
    [ProducesResponseType<SignalIdentity[]>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListIdentities([FromQuery] string number)
        => Ok(await signalCliRestClientSvc.ListIdentities(number));

    /// <inheritdoc cref="SignalCliRestClientService.ListAttachments"/>
    [HttpGet("attachments")]
    [ProducesResponseType<string[]>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAttachments()
        => Ok(await signalCliRestClientSvc.ListAttachments());

    /// <inheritdoc cref="SignalCliRestClientService.ListStickerPacks"/>
    [HttpGet("sticker-packs")]
    [ProducesResponseType<SignalStickerPack[]>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListStickerPacks([FromQuery] string number)
        => Ok(await signalCliRestClientSvc.ListStickerPacks(number));
}

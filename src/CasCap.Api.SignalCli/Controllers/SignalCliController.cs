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
    public async Task<Ok<SignalAbout>> GetAbout()
        => TypedResults.Ok(await signalCliRestClientSvc.GetAbout());

    /// <inheritdoc cref="SignalCliRestClientService.GetConfiguration"/>
    [HttpGet("configuration")]
    public async Task<Ok<SignalConfiguration>> GetConfiguration()
        => TypedResults.Ok(await signalCliRestClientSvc.GetConfiguration());

    /// <inheritdoc cref="SignalCliRestClientService.ListAccounts"/>
    [HttpGet("accounts")]
    public async Task<Ok<string[]>> ListAccounts()
        => TypedResults.Ok(await signalCliRestClientSvc.ListAccounts());

    /// <inheritdoc cref="SignalCliRestClientService.ListContacts"/>
    [HttpGet("contacts")]
    public async Task<Ok<SignalContact[]>> ListContacts([FromQuery] string number)
        => TypedResults.Ok(await signalCliRestClientSvc.ListContacts(number));

    /// <inheritdoc cref="SignalCliRestClientService.ListGroups"/>
    [HttpGet("groups")]
    public async Task<Ok<SignalGroup[]>> ListGroups([FromQuery] string number)
        => TypedResults.Ok(await signalCliRestClientSvc.ListGroups(number));

    /// <inheritdoc cref="SignalCliRestClientService.ListLinkedDevices"/>
    [HttpGet("devices")]
    public async Task<Ok<SignalDevice[]>> ListLinkedDevices([FromQuery] string number)
        => TypedResults.Ok(await signalCliRestClientSvc.ListLinkedDevices(number));

    /// <inheritdoc cref="SignalCliRestClientService.ListIdentities"/>
    [HttpGet("identities")]
    public async Task<Ok<SignalIdentity[]>> ListIdentities([FromQuery] string number)
        => TypedResults.Ok(await signalCliRestClientSvc.ListIdentities(number));

    /// <inheritdoc cref="SignalCliRestClientService.ListAttachments"/>
    [HttpGet("attachments")]
    public async Task<Ok<string[]>> ListAttachments()
        => TypedResults.Ok(await signalCliRestClientSvc.ListAttachments());

    /// <inheritdoc cref="SignalCliRestClientService.ListStickerPacks"/>
    [HttpGet("sticker-packs")]
    public async Task<Ok<SignalStickerPack[]>> ListStickerPacks([FromQuery] string number)
        => TypedResults.Ok(await signalCliRestClientSvc.ListStickerPacks(number));
}

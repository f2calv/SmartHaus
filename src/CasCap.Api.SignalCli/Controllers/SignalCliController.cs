namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Signal CLI service queries.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public sealed class SignalCliController(SignalCliRestClientService signalCliRestClientSvc) : ControllerBase
{
    /// <inheritdoc cref="SignalCliRestClientService.GetAbout"/>
    [HttpGet("about")]
    public async Task<Results<Ok<SignalAbout>, NotFound>> GetAbout()
        => await signalCliRestClientSvc.GetAbout() is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();

    /// <inheritdoc cref="SignalCliRestClientService.GetConfiguration"/>
    [HttpGet("configuration")]
    public async Task<Results<Ok<SignalConfiguration>, NotFound>> GetConfiguration()
        => await signalCliRestClientSvc.GetConfiguration() is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();

    /// <inheritdoc cref="SignalCliRestClientService.ListAccounts"/>
    [HttpGet("accounts")]
    public async Task<Results<Ok<string[]>, NotFound>> ListAccounts()
        => await signalCliRestClientSvc.ListAccounts() is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();

    /// <inheritdoc cref="SignalCliRestClientService.ListContacts"/>
    [HttpGet("contacts")]
    public async Task<Results<Ok<SignalContact[]>, NotFound>> ListContacts([FromQuery] string number)
        => await signalCliRestClientSvc.ListContacts(number) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();

    /// <inheritdoc cref="SignalCliRestClientService.ListGroups"/>
    [HttpGet("groups")]
    public async Task<Results<Ok<SignalGroup[]>, NotFound>> ListGroups([FromQuery] string number)
        => await signalCliRestClientSvc.ListGroups(number) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();

    /// <inheritdoc cref="SignalCliRestClientService.ListLinkedDevices"/>
    [HttpGet("devices")]
    public async Task<Results<Ok<SignalDevice[]>, NotFound>> ListLinkedDevices([FromQuery] string number)
        => await signalCliRestClientSvc.ListLinkedDevices(number) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();

    /// <inheritdoc cref="SignalCliRestClientService.ListIdentities"/>
    [HttpGet("identities")]
    public async Task<Results<Ok<SignalIdentity[]>, NotFound>> ListIdentities([FromQuery] string number)
        => await signalCliRestClientSvc.ListIdentities(number) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();

    /// <inheritdoc cref="SignalCliRestClientService.ListAttachments"/>
    [HttpGet("attachments")]
    public async Task<Results<Ok<string[]>, NotFound>> ListAttachments()
        => await signalCliRestClientSvc.ListAttachments() is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();

    /// <inheritdoc cref="SignalCliRestClientService.ListStickerPacks"/>
    [HttpGet("sticker-packs")]
    public async Task<Results<Ok<SignalStickerPack[]>, NotFound>> ListStickerPacks([FromQuery] string number)
        => await signalCliRestClientSvc.ListStickerPacks(number) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();
}

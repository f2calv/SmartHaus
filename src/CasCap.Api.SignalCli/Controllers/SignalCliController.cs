namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Signal CLI service queries.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public sealed class SignalCliController(ISignalCliClient signalCliClient) : ControllerBase
{
    /// <inheritdoc cref="ISignalCliClient.GetAbout"/>
    [HttpGet("about")]
    public async Task<Results<Ok<SignalAbout>, NotFound>> GetAbout(CancellationToken cancellationToken)
        => await signalCliClient.GetAbout(cancellationToken) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();

    /// <inheritdoc cref="ISignalCliClient.GetConfiguration"/>
    [HttpGet("configuration")]
    public async Task<Results<Ok<SignalConfiguration>, NotFound>> GetConfiguration(CancellationToken cancellationToken)
        => await signalCliClient.GetConfiguration(cancellationToken) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();

    /// <inheritdoc cref="ISignalCliClient.ListAccounts"/>
    [HttpGet("accounts")]
    public async Task<Results<Ok<string[]>, NotFound>> ListAccounts(CancellationToken cancellationToken)
        => await signalCliClient.ListAccounts(cancellationToken) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();

    /// <inheritdoc cref="ISignalCliClient.ListContacts"/>
    [HttpGet("contacts")]
    public async Task<Results<Ok<SignalContact[]>, NotFound>> ListContacts([FromQuery] string number, CancellationToken cancellationToken)
        => await signalCliClient.ListContacts(number, cancellationToken: cancellationToken) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();

    /// <inheritdoc cref="ISignalCliClient.ListGroups"/>
    [HttpGet("groups")]
    public async Task<Results<Ok<SignalGroup[]>, NotFound>> ListGroups([FromQuery] string number, CancellationToken cancellationToken)
        => await signalCliClient.ListGroups(number, cancellationToken) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();

    /// <inheritdoc cref="ISignalCliClient.ListLinkedDevices"/>
    [HttpGet("devices")]
    public async Task<Results<Ok<SignalDevice[]>, NotFound>> ListLinkedDevices([FromQuery] string number, CancellationToken cancellationToken)
        => await signalCliClient.ListLinkedDevices(number, cancellationToken) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();

    /// <inheritdoc cref="ISignalCliClient.ListIdentities"/>
    [HttpGet("identities")]
    public async Task<Results<Ok<SignalIdentity[]>, NotFound>> ListIdentities([FromQuery] string number, CancellationToken cancellationToken)
        => await signalCliClient.ListIdentities(number, cancellationToken) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();

    /// <inheritdoc cref="ISignalCliClient.ListAttachments"/>
    [HttpGet("attachments")]
    public async Task<Results<Ok<string[]>, NotFound>> ListAttachments(CancellationToken cancellationToken)
        => await signalCliClient.ListAttachments(cancellationToken) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();

    /// <inheritdoc cref="ISignalCliClient.ListStickerPacks"/>
    [HttpGet("sticker-packs")]
    public async Task<Results<Ok<SignalStickerPack[]>, NotFound>> ListStickerPacks([FromQuery] string number, CancellationToken cancellationToken)
        => await signalCliClient.ListStickerPacks(number, cancellationToken) is { } result
            ? TypedResults.Ok(result)
            : TypedResults.NotFound();
}

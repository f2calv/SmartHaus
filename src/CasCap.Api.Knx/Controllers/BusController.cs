namespace CasCap.Controllers;

/// <summary>
/// REST API controller for KNX bus queries and commands.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class BusController(IKnxQueryService knxQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="KnxQueryService.GetGroupAddresses"/>
    [HttpGet("addresses")]
    public async Task<Ok<IEnumerable<KnxGroupAddressParsed>>> GetGroupAddresses([FromQuery] GroupAddressFilter groupAddressFilter = GroupAddressFilter.None, CancellationToken cancellationToken = default)
        => TypedResults.Ok(await knxQuerySvc.GetGroupAddresses(groupAddressFilter, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.FilterGroupAddresses"/>
    [HttpGet("addresses/filter")]
    public async Task<Ok<List<string>>> FilterGroupAddresses([FromQuery] string? category = null, [FromQuery] string? floor = null, [FromQuery] string? orientation = null, [FromQuery] string? function = null, CancellationToken cancellationToken = default)
        => TypedResults.Ok(await knxQuerySvc.FilterGroupAddresses(category, floor, orientation, function, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.GetGroupAddressesGrouped"/>
    [HttpGet("addresses/grouped")]
    public async Task<Ok<List<KnxGroupAddressGroup>>> GetGroupAddressesGrouped([FromQuery] GroupAddressFilter groupAddressFilter = GroupAddressFilter.None, CancellationToken cancellationToken = default)
        => TypedResults.Ok(await knxQuerySvc.GetGroupAddressesGrouped(groupAddressFilter, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.GetGroupAddressesRaw"/>
    [HttpGet("addresses/raw")]
    public async Task<Ok<List<KnxGroupAddressXml>>> GetGroupAddressesRaw(CancellationToken cancellationToken = default)
        => TypedResults.Ok(await knxQuerySvc.GetGroupAddressesRaw(cancellationToken));

    /// <inheritdoc cref="KnxQueryService.ListFloors"/>
    [HttpGet("floors")]
    public async Task<Ok<List<KnxRoom>>> ListFloors(CancellationToken cancellationToken = default)
        => TypedResults.Ok(await knxQuerySvc.ListFloors(cancellationToken));

    /// <inheritdoc cref="KnxQueryService.ListRooms"/>
    [HttpGet("rooms")]
    public async Task<Ok<List<KnxRoom>>> ListRooms(CancellationToken cancellationToken = default)
        => TypedResults.Ok(await knxQuerySvc.ListRooms(cancellationToken));

    /// <inheritdoc cref="KnxQueryService.GetAllState"/>
    [HttpGet("state")]
    public async Task<Ok<Dictionary<string, State>>> GetAllState(CancellationToken cancellationToken = default)
        => TypedResults.Ok(await knxQuerySvc.GetAllState(cancellationToken));

    /// <inheritdoc cref="KnxQueryService.ValidateGroupAddress"/>
    [HttpGet("validate/address/{groupAddress}")]
    public async Task<Results<Ok<KnxGroupAddressParsed>, NotFound>> ValidateGroupAddress(string groupAddress, CancellationToken cancellationToken = default)
    {
        var result = await knxQuerySvc.ValidateGroupAddress(groupAddress, cancellationToken);
        return result is not null ? TypedResults.Ok(result) : TypedResults.NotFound();
    }

    /// <inheritdoc cref="KnxQueryService.ValidateGroupName"/>
    [HttpGet("validate/group/{groupName}")]
    public async Task<Results<Ok<KnxGroupAddressGroup>, NotFound>> ValidateGroupName(string groupName, CancellationToken cancellationToken = default)
    {
        var result = await knxQuerySvc.ValidateGroupName(groupName, cancellationToken);
        return result is not null ? TypedResults.Ok(result) : TypedResults.NotFound();
    }

    /// <inheritdoc cref="KnxQueryService.Send2Bus"/>
    [HttpPost("state/send")]
    public async Task<Results<Ok<State>, NotFound>> Send2Bus([FromBody] KnxStateChangeRequest request, CancellationToken cancellationToken = default)
    {
        var result = await knxQuerySvc.Send2Bus(request, cancellationToken);
        return result is not null ? TypedResults.Ok(result) : TypedResults.NotFound();
    }

    }

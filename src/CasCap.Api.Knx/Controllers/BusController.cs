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
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroupAddresses([FromQuery] GroupAddressFilter groupAddressFilter = GroupAddressFilter.None, CancellationToken cancellationToken = default)
        => Ok(await knxQuerySvc.GetGroupAddresses(groupAddressFilter, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.FilterGroupAddresses"/>
    [HttpGet("addresses/filter")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> FilterGroupAddresses([FromQuery] string? category = null, [FromQuery] string? floor = null, [FromQuery] string? orientation = null, [FromQuery] string? function = null, CancellationToken cancellationToken = default)
        => Ok(await knxQuerySvc.FilterGroupAddresses(category, floor, orientation, function, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.GetGroupAddressesGrouped"/>
    [HttpGet("addresses/grouped")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroupAddressesGrouped([FromQuery] GroupAddressFilter groupAddressFilter = GroupAddressFilter.None, CancellationToken cancellationToken = default)
        => Ok(await knxQuerySvc.GetGroupAddressesGrouped(groupAddressFilter, cancellationToken));

    /// <inheritdoc cref="KnxQueryService.GetGroupAddressesRaw"/>
    [HttpGet("addresses/raw")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroupAddressesRaw(CancellationToken cancellationToken = default)
        => Ok(await knxQuerySvc.GetGroupAddressesRaw(cancellationToken));

    /// <inheritdoc cref="KnxQueryService.ListFloors"/>
    [HttpGet("floors")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFloors(CancellationToken cancellationToken = default)
        => Ok(await knxQuerySvc.ListFloors(cancellationToken));

    /// <inheritdoc cref="KnxQueryService.ListRooms"/>
    [HttpGet("rooms")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRooms(CancellationToken cancellationToken = default)
        => Ok(await knxQuerySvc.ListRooms(cancellationToken));

    /// <inheritdoc cref="KnxQueryService.GetAllState"/>
    [HttpGet("state")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllState(CancellationToken cancellationToken = default)
        => Ok(await knxQuerySvc.GetAllState(cancellationToken));

    /// <inheritdoc cref="KnxQueryService.ValidateGroupAddress"/>
    [HttpGet("validate/address/{groupAddress}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ValidateGroupAddress(string groupAddress, CancellationToken cancellationToken = default)
    {
        var result = await knxQuerySvc.ValidateGroupAddress(groupAddress, cancellationToken);
        return result is not null ? Ok(result) : NotFound();
    }

    /// <inheritdoc cref="KnxQueryService.ValidateGroupName"/>
    [HttpGet("validate/group/{groupName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ValidateGroupName(string groupName, CancellationToken cancellationToken = default)
    {
        var result = await knxQuerySvc.ValidateGroupName(groupName, cancellationToken);
        return result is not null ? Ok(result) : NotFound();
    }

    /// <inheritdoc cref="KnxQueryService.Send2Bus"/>
    [HttpPost("state/send")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Send2Bus([FromBody] KnxStateChangeRequest request, CancellationToken cancellationToken = default)
    {
        var result = await knxQuerySvc.Send2Bus(request, cancellationToken);
        return result is not null ? Ok(result) : NotFound();
    }

    }

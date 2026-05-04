using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CasCap.Controllers;

/// <summary>Returns build and deployment metadata.</summary>
[ApiController]
[Route("api/[controller]")]
public class SystemController(ILogger<SystemController> logger, GitMetadata gitMetadata) : ControllerBase
{
    /// <summary>Returns git build information.</summary>
    [Authorize]
    [HttpGet]
    [ProducesResponseType<GitMetadata>(StatusCodes.Status200OK)]
    public Ok<GitMetadata> Get()
    {
        logger.LogDebug("{ClassName} returning git info", nameof(SystemController));
        return TypedResults.Ok(gitMetadata);
    }
}

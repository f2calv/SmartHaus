using Microsoft.AspNetCore.Authorization;

namespace CasCap.Controllers;

/// <summary>Returns build and deployment metadata.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class SystemController(ILogger<SystemController> logger, ApplicationMetadata applicationMetadata) : ControllerBase
{
    /// <summary>Returns application build information.</summary>
    [Authorize]
    [HttpGet]
    public Ok<ApplicationMetadata> Get()
    {
        logger.LogDebug("{ClassName} returning application metadata", nameof(SystemController));
        return TypedResults.Ok(applicationMetadata);
    }
}

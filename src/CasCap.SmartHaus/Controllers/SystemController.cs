using Microsoft.AspNetCore.Authorization;

namespace CasCap.Controllers;

/// <summary>Returns build and deployment metadata.</summary>
[ApiController]
[Route("api/[controller]")]
public class SystemController(ILogger<SystemController> logger, GitMetadata gitMetadata) : ControllerBase
{
    /// <summary>Returns git build information.</summary>
    [Authorize]
    [HttpGet]
    public GitMetadata Get()
    {
        logger.LogDebug("{ClassName} returning git info", nameof(SystemController));
        return gitMetadata;
    }
}

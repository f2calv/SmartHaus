using ModelContextProtocol.AspNetCore;

namespace CasCap;

public static partial class AppHost
{
    private static void MapMcp(WebApplication app, AppConfig appConfig)
    {
        app.MapGroup(appConfig.McpUrl)
            .RequireAuthorization()
            .MapMcp();
    }
}

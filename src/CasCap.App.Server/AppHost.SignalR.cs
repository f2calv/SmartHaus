using CasCap.Constants;

namespace CasCap;

public static partial class AppHost
{
    private static void MapSignalR(
        WebApplication app,
        IReadOnlySet<string> enabledFeatures,
        SignalRHubConfig? signalRHubConfig)
    {
        if (enabledFeatures.Contains(FeatureNames.SignalRHub))
            app.MapHub<HausHub>(signalRHubConfig!.HubPath).RequireAuthorization();
    }
}

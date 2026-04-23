namespace CasCap.Services;

/// <summary>
/// Facade service for Wiz smart bulb operations, delegating to <see cref="WizClientService"/>.
/// </summary>
public class WizQueryService(
    ILogger<WizQueryService> logger,
    WizClientService clientSvc) : IWizQueryService
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<string, WizBulb> GetDiscoveredBulbs()
    {
        logger.LogDebug("{ClassName} returning discovered bulbs", nameof(WizQueryService));
        return clientSvc.DiscoveredBulbs;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, WizBulb>> DiscoverBulbs(CancellationToken cancellationToken)
    {
        logger.LogDebug("{ClassName} triggering on-demand bulb discovery", nameof(WizQueryService));
        return await clientSvc.DiscoverBulbsAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<WizPilotState?> GetPilot(string bulbIdentifier, CancellationToken cancellationToken)
    {
        var ip = ResolveIpOrThrow(bulbIdentifier);
        logger.LogDebug("{ClassName} getting pilot state for {BulbIp}", nameof(WizQueryService), ip);
        return await clientSvc.GetPilotAsync(ip, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<WizSystemConfig?> GetSystemConfig(string bulbIdentifier, CancellationToken cancellationToken)
    {
        var ip = ResolveIpOrThrow(bulbIdentifier);
        logger.LogDebug("{ClassName} getting system config for {BulbIp}", nameof(WizQueryService), ip);
        return await clientSvc.GetSystemConfigAsync(ip, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> SetPilot(string bulbIdentifier, WizSetPilotRequest request, CancellationToken cancellationToken)
    {
        var ip = ResolveIpOrThrow(bulbIdentifier);
        logger.LogInformation("{ClassName} setting pilot state for {BulbIp}", nameof(WizQueryService), ip);
        return await clientSvc.SetPilotAsync(ip, request, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> SetPowerState(string bulbIdentifier, bool on, CancellationToken cancellationToken)
    {
        var ip = ResolveIpOrThrow(bulbIdentifier);
        logger.LogInformation("{ClassName} setting power state for {BulbIp} to {PowerState}",
            nameof(WizQueryService), ip, on);
        return await clientSvc.SetPilotAsync(ip, new WizSetPilotRequest { State = on }, cancellationToken);
    }

    private string ResolveIpOrThrow(string bulbIdentifier) =>
        clientSvc.ResolveToIp(bulbIdentifier)
        ?? throw new ArgumentException($"Bulb '{bulbIdentifier}' not found. Use a discovered IP, MAC, or configured device name.");
}

using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Resources;

namespace CasCap.Services;

/// <summary>
/// Finds out the current DSL IP and updates the Azure DNS records accordingly.
/// <remarks>See <see href="https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/dns/Azure.ResourceManager.Dns" />.</remarks>
/// <remarks>See <see href="https://github.com/Azure/azure-sdk-for-net/blob/main/doc/dev/mgmt_quickstart.md" />.</remarks>
/// </summary>
public class DDnsBgService(
    ILogger<DDnsBgService> logger,
    IOptions<RedlockConfig> redlockConfig,
    IOptions<AzureAuthConfig> azureAuthConfig,
    IOptions<DDnsConfig> dDnsConfig,
    DDnsFindMyIpClientService findMyIpClientSvc,
    IDistributedLockFactory lockFactory,
    IEventSink<CommsEvent> commsSink
    ) : IBgFeature
{
    private readonly SubscriptionCollection _subscriptions = new ArmClient(azureAuthConfig.Value.TokenCredential).GetSubscriptions();

    /// <inheritdoc/>
    public string FeatureName => DDnsConfig.FeatureName;

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} starting", nameof(DDnsBgService));
        try
        {
            await AcquireLock(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException) { throw; }
        logger.LogInformation("{ClassName} exiting", nameof(DDnsBgService));
    }

    private async Task AcquireLock(CancellationToken cancellationToken)
    {
        var resource = "ddns";
        var (expiry, wait, retry) = redlockConfig.Value.GetTimings(RedlockProfiles.LeaderElection);

        var lockAttempt = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            await using (var redLock = await lockFactory.CreateLockAsync(resource, expiry, wait, retry, cancellationToken))
            {
                if (redLock.IsAcquired)
                {
                    logger.LogInformation("{ClassName} distributed lock acquired on attempt {Attempt}",
                        nameof(DDnsBgService), lockAttempt);
                    await RunServiceAsync(cancellationToken);
                    logger.LogInformation("{ClassName} distributed lock released", nameof(DDnsBgService));
                }
                else
                {
                    lockAttempt++;
                    logger.LogInformation("{ClassName} distributed lock failed on attempt {Attempt}",
                        nameof(DDnsBgService), lockAttempt);
                }
            }
            logger.LogInformation("{ClassName} distributed lock pending attempt {Attempt}",
                nameof(DDnsBgService), lockAttempt + 1);
        }
    }

    private async Task RunServiceAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var currentIpAddress = await findMyIpClientSvc.GetIp(cancellationToken);
            if (currentIpAddress is not null && (oldIp is null || oldIp.ToString() != currentIpAddress.ToString()))
            {
                await UpdateListDnsZoneResources(currentIpAddress, cancellationToken);
            }
            await Task.Delay(dDnsConfig.Value.RefreshDelayMs, cancellationToken);
        }
    }

    private SubscriptionResource? subscription;
    private IPAddress? oldIp;

    private async Task UpdateListDnsZoneResources(IPAddress newIp, CancellationToken cancellationToken)
    {
        await SetSubscription();
        if (subscription is null)
            return;

        var aRecords = await GetDnsARecordCollection(cancellationToken);
        if (aRecords is null)
            return;

        IPAddress? priorIp = null;
        var updatedRecords = new List<string>();
        foreach (var aRecord in aRecords)
        {
            var doUpdate = false;
            var data = aRecord.Data;
            if (data.Metadata is not null
                && data.Metadata.Any(p => p.Key.Equals(dDnsConfig.Value.DnsMetaDataKey, StringComparison.OrdinalIgnoreCase)))
            {
                //iterate over the IPs within the A record - as you can have multiple!
                foreach (var record in data.DnsARecords)
                {
                    priorIp = record.IPv4Address;
                    if (!priorIp.Equals(newIp))
                    {
                        record.IPv4Address = newIp;
                        doUpdate = true;
                    }
                }
            }

            if (doUpdate)
            {
                _ = await aRecords.CreateOrUpdateAsync(WaitUntil.Completed, data.Name, data, cancellationToken: cancellationToken);
                logger.LogInformation("{ClassName} DNS A Record {ARecordName} updated from {PriorIp} to {NewIp}",
                    nameof(DDnsBgService), data.Name, priorIp, newIp);
                updatedRecords.Add(data.Name);
            }
        }

        if (updatedRecords.Count > 0)
        {
            var recordList = string.Join(", ", updatedRecords);
            var commsEvent = new CommsEvent
            {
                Source = nameof(DDnsBgService),
                Message = $"DNS updated {updatedRecords.Count} A record(s) from {priorIp} to {newIp}: {recordList}",
                TimestampUtc = DateTime.UtcNow,
            };
            await commsSink.WriteEvent(commsEvent, cancellationToken);
        }

        //now we have completed all A records update the static
        oldIp = priorIp;

        async Task SetSubscription()
        {
            logger.LogDebug("{ClassName} querying Azure for correct subscription...",
                nameof(DDnsBgService));
            try
            {
                foreach (var sub in _subscriptions)
                {
                    var rg = await sub.GetResourceGroupAsync(dDnsConfig.Value.DnsResourceGroupName, cancellationToken);
                    if (rg.GetRawResponse().Status == 200)
                    {
                        subscription = sub;
                        logger.LogInformation("{ClassName} selected Azure subscription {DisplayName} ({SubscriptionId})",
                            nameof(DDnsBgService), sub.Data.DisplayName, sub.Data.SubscriptionId);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{ClassName} Azure authentication failure", nameof(DDnsBgService));
            }

            if (subscription is null)
                logger.LogError("{ClassName} unable to find subscription containing Resource Group {DnsResourceGroupName}",
                    nameof(DDnsBgService), dDnsConfig.Value.DnsResourceGroupName);
        }
    }

    private async Task<DnsARecordCollection?> GetDnsARecordCollection(CancellationToken cancellationToken)
    {
        // first we need to get the resource group
        ResourceGroupResource resourceGroup;
        try
        {
            resourceGroup = await subscription!.GetResourceGroupAsync(dDnsConfig.Value.DnsResourceGroupName, cancellationToken);
            logger.LogDebug("{ClassName} Azure Resource Group {DnsResourceGroupName} located",
                nameof(DDnsBgService), dDnsConfig.Value.DnsResourceGroupName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{ClassName} Azure Resource Group {DnsResourceGroupName} not found",
                nameof(DDnsBgService), dDnsConfig.Value.DnsResourceGroupName);
            return null;
        }

        // Now we get the DnsZone collection from the resource group
        DnsZoneResource dnsZone;
        try
        {
            dnsZone = await resourceGroup.GetDnsZoneAsync(dDnsConfig.Value.DnsZoneName, cancellationToken);
            return dnsZone.GetDnsARecords();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{ClassName} Azure DNS Zone {DnsZoneName} not found",
                nameof(DDnsBgService), dDnsConfig.Value.DnsZoneName);
            return null;
        }
    }
}

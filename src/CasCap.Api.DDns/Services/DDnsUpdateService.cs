using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Hosting;

namespace CasCap.Services;

/// <summary>
/// Finds out the current DSL IP and updates the Azure DNS records accordingly.
/// <remarks>See <see href="https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/dns/Azure.ResourceManager.Dns" />.</remarks>
/// <remarks>See <see href="https://github.com/Azure/azure-sdk-for-net/blob/main/doc/dev/mgmt_quickstart.md" />.</remarks>
/// </summary>
public sealed class DDnsUpdateService(
    ILogger<DDnsUpdateService> logger,
    IOptions<AzureAuthConfig> azureAuthConfig,
    IOptions<DDnsConfig> dDnsConfig,
    TimeProvider timeProvider,
    DDnsFindMyIpClientService findMyIpClientSvc,
    IHostEnvironment env,
    IEventSink<CommsEvent> commsSink
    )
{
    /// <summary>Revalidates DNS state for a new ownership run and polls until leadership cancellation.</summary>
    /// <param name="cancellationToken">The current leadership run's cancellation token.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        subscription = null;
        oldIp = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            var currentIpAddress = await findMyIpClientSvc.GetIp(cancellationToken);
            if (currentIpAddress is not null && (oldIp is null || oldIp.ToString() != currentIpAddress.ToString()))
            {
                await UpdateListDnsZoneResources(currentIpAddress, cancellationToken);
            }
            await Task.Delay(TimeSpan.FromMilliseconds(dDnsConfig.Value.RefreshDelayMs), timeProvider, cancellationToken);
        }
    }

    private SubscriptionResource? subscription;
    private IPAddress? oldIp;

    private async Task UpdateListDnsZoneResources(IPAddress newIp, CancellationToken cancellationToken)
    {
        await SetSubscriptionAsync(cancellationToken);
        if (subscription is null)
            return;

        var aRecords = await GetDnsARecordCollection(cancellationToken);
        if (aRecords is null)
            return;

        IPAddress? priorIp = null;
        var updatedRecords = new List<string>();
        await foreach (var aRecord in aRecords.GetAllAsync(cancellationToken: cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                cancellationToken.ThrowIfCancellationRequested();
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
                Environment = env.GetAcronym(),
                TimestampUtc = timeProvider.GetUtcNow().UtcDateTime,
            };
            await commsSink.WriteEvent(commsEvent, cancellationToken);
        }

        //now we have completed all A records update the static
        oldIp = priorIp;
    }

    private async Task SetSubscriptionAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("{ClassName} querying Azure for correct subscription...",
            nameof(DDnsBgService));
        var subscriptions = new ArmClient(azureAuthConfig.Value.TokenCredential).GetSubscriptions();
        await foreach (var sub in subscriptions.GetAllAsync(cancellationToken: cancellationToken))
        {
            try
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
            catch (RequestFailedException ex) when (ex.Status is StatusCodes.Status403Forbidden
                or StatusCodes.Status404NotFound)
            {
                logger.LogWarning("{ClassName} cannot access Resource Group {DnsResourceGroupName} in Azure subscription {SubscriptionId}, StatusCode={StatusCode}",
                    nameof(DDnsBgService), dDnsConfig.Value.DnsResourceGroupName,
                    sub.Data.SubscriptionId, ex.Status);
            }
        }

        if (subscription is null)
            logger.LogError("{ClassName} unable to find subscription containing Resource Group {DnsResourceGroupName}",
                nameof(DDnsBgService), dDnsConfig.Value.DnsResourceGroupName);
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
        catch (Exception ex) when (ex is not OperationCanceledException)
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "{ClassName} Azure DNS Zone {DnsZoneName} not found",
                nameof(DDnsBgService), dDnsConfig.Value.DnsZoneName);
            return null;
        }
    }
}

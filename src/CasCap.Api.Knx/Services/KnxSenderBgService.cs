using Knx.Falcon;

namespace CasCap.Services;

/// <summary>
/// This service handles sending outbound telegrams to the KNX bus, these outbound telegrams are
/// dequeued from the <see cref="IKnxTelegramBroker{T}"/> for <see cref="KnxOutgoingTelegram"/>.
/// </summary>
public class KnxSenderBgService(ILogger<KnxSenderBgService> logger, IOptions<KnxConfig> config, IKnxTelegramBroker<KnxOutgoingTelegram> outgoingBroker) : IBgFeature
{
    /// <inheritdoc/>
    public string FeatureName => "Knx";

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} starting", nameof(KnxSenderBgService));
        try
        {
            await RunServiceAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException) { throw; }
        logger.LogInformation("{ClassName} exiting", nameof(KnxSenderBgService));
    }

    private Task RunServiceAsync(CancellationToken cancellationToken) => RunSender(cancellationToken);

    private async Task RunSender(CancellationToken cancellationToken)
    {
        await foreach (var msg in outgoingBroker.SubscribeAsync(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested) break;

            var busAddresses = new List<string>(KnxStatics.BusConnections.Count);
            foreach (var busConnection in KnxStatics.BusConnections)
            {
                var bus = busConnection.Value;
                if (bus is null || bus.ConnectionState != BusConnectionState.Connected)
                {
                    logger.LogError("{ClassName} unable to send to {GroupAddress} as bus connection {Bus} broken!",
                        nameof(KnxSenderBgService), msg.Kga.Name, busConnection.Key);
                    continue;
                }
                if (config.Value.BusSenderLoggingEnabled
                    && (string.IsNullOrEmpty(config.Value.BusLoggingGroupAddressFilter)
                        || msg.Kga.Name.Contains(config.Value.BusLoggingGroupAddressFilter, StringComparison.OrdinalIgnoreCase)))
                    busAddresses.Add(bus.InterfaceConfiguration.IndividualAddress);

                var groupAddress = new GroupAddress(msg.Kga.GroupAddress);
                var dptBase = msg.Kga.GetDptBase();
                var groupValue = dptBase.SizeInBit < 8 && msg.GroupValueData.Length == 1
                    ? new GroupValue(msg.GroupValueData[0], (int)dptBase.SizeInBit)
                    : new GroupValue(msg.GroupValueData);
                await bus.WriteGroupValueAsync(groupAddress, groupValue, cancellationToken: cancellationToken);
            }

            // Only log if we sent to at least one bus, otherwise the error logs above should be sufficient
            if (busAddresses.Count > 0)
                logger.LogInformation("{ClassName} sending telegram from {@BusAddresses} to {GroupAddressName} with value {GroupValueData}",
                    nameof(KnxSenderBgService), busAddresses, msg.Kga.Name, msg.GroupValueData);
        }
    }
}

using Knx.Falcon;

namespace CasCap.Services;

/// <summary>
/// This service handles sending outbound telegrams to the KNX bus, these outbound telegrams are
/// dequeued from the <see cref="IKnxTelegramBroker{T}"/> for <see cref="KnxOutgoingTelegram"/>.
/// </summary>
public sealed partial class KnxSenderBgService(
    ILogger<KnxSenderBgService> logger,
    IOptions<KnxConfig> config,
    TimeProvider timeProvider,
    KnxConnectionHealthCheck knxConnectionHealthCheck,
    IKnxTelegramBroker<KnxOutgoingTelegram> outgoingBroker)
{
    /// <summary>Consumes and sends outbound telegrams only for the current bus ownership run.</summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        await foreach (var msg in outgoingBroker.SubscribeAsync(cancellationToken))
        {
            await WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            await SendTelegramAsync(msg, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendTelegramAsync(KnxOutgoingTelegram msg, CancellationToken cancellationToken)
    {
        var busAddresses = new List<string>(KnxStatics.BusConnections.Count);
        foreach (var busConnection in KnxStatics.BusConnections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bus = busConnection.Value;
            if (bus is null || bus.ConnectionState != BusConnectionState.Connected)
            {
                LogBusConnectionBroken(logger, nameof(KnxSenderBgService), msg.Kga.Name, busConnection.Key.ToString());
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
            await bus.WriteGroupValueAsync(groupAddress, groupValue, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // Only log if we sent to at least one bus, otherwise the error logs above should be sufficient
        if (busAddresses.Count > 0)
            LogTelegramSent(logger, nameof(KnxSenderBgService), busAddresses.Count, msg.Kga.Name);
    }

    private async Task WaitForConnectionAsync(CancellationToken cancellationToken)
    {
        while (!knxConnectionHealthCheck.ConnectionActive
            && !KnxStatics.BusConnections.Values.Any(bus => bus?.ConnectionState == BusConnectionState.Connected))
            await Task.Delay(TimeSpan.FromMilliseconds(config.Value.ConnectionPollingDelayMs), timeProvider, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{ClassName} unable to send to {GroupAddress} as bus connection {Bus} broken!")]
    private static partial void LogBusConnectionBroken(ILogger logger, string className, string groupAddress, string bus);

    [LoggerMessage(Level = LogLevel.Information, Message = "{ClassName} sending telegram from {BusCount} bus(es) to {GroupAddressName}")]
    private static partial void LogTelegramSent(ILogger logger, string className, int busCount, string groupAddressName);
}

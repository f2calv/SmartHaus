namespace CasCap.Services;

/// <summary>
/// The <see cref="KnxProcessorBgService"/> subscribes to the telegrams arriving via the
/// <see cref="IKnxTelegramBroker{T}"/> and then sends them to each of the configured
/// <see cref="IEventSink{KnxEvent}"/> instances for processing (e.g. writing to Redis,
/// sending alerts, etc).
/// </summary>
public sealed partial class KnxProcessorBgService(
    ILogger<KnxProcessorBgService> logger,
    IEnumerable<IEventSink<KnxEvent>> eventSinks,
    KnxConnectionHealthCheck knxConnectionHealthCheck,
    IKnxTelegramBroker<KnxEvent> incomingBroker
    ) : IBgFeature
{
    /// <inheritdoc/>
    public string FeatureName => "Knx";

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (eventSinks.IsNullOrEmpty()) throw new GenericException($"no {nameof(IEventSink<KnxEvent>)} is configured!");

        //wait for the KNX bus connection to be active (which implies group addresses are loaded)
        while (!knxConnectionHealthCheck.ConnectionActive && !cancellationToken.IsCancellationRequested)
            await Task.Delay(1_000, cancellationToken);

        foreach (var eventSink in eventSinks)
            await eventSink.InitializeAsync(cancellationToken);

        var sinkTasks = new List<Task>(eventSinks.Count());
        await foreach (var knxEvent in incomingBroker.SubscribeAsync(cancellationToken))
        {
            if (knxEvent.Kga is null)
            {
                LogKgaNull(logger, nameof(KnxProcessorBgService));
                continue;
            }
            else if (knxEvent.Kga.Category == GroupAddressCategory.Unknown)
            {
                LogUnknownGroupAddress(logger, nameof(KnxProcessorBgService), knxEvent.Kga.Name, knxEvent.Kga.GroupAddress);
                continue;
            }

            foreach (var eventSink in eventSinks)
                sinkTasks.Add(eventSink.WriteEvent(knxEvent, cancellationToken));
            await Task.WhenAll(sinkTasks);
            sinkTasks.Clear();
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{ClassName} knxEvent.Kga is null?")]
    private static partial void LogKgaNull(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{ClassName} GA '{Name} {Address}' does not conform to GA naming convention, we will ignore this GA...")]
    private static partial void LogUnknownGroupAddress(ILogger logger, string className, string name, string address);
}

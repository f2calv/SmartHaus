namespace CasCap.Services;

/// <summary>
/// The <see cref="KnxProcessorBgService"/> subscribes to the telegrams arriving via the
/// <see cref="IKnxTelegramBroker{T}"/> and then sends them to each of the configured
/// <see cref="IEventSink{KnxEvent}"/> instances for processing (e.g. writing to Redis,
/// sending alerts, etc).
/// </summary>
public class KnxProcessorBgService(
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
                logger.LogError("knxEvent.Kga is null?");
                continue;
            }
            else if (knxEvent.Kga.Category == GroupAddressCategory.Unknown)
            {
                logger.LogWarning("{ClassName} GA '{Name} {Address}' does not conform to GA naming convention, we will ignore this GA...",
                    nameof(KnxProcessorBgService), knxEvent.Kga.Name, knxEvent.Kga.GroupAddress);
                continue;
            }

            foreach (var eventSink in eventSinks)
                sinkTasks.Add(eventSink.WriteEvent(knxEvent, cancellationToken));
            //Parallel.ForEach(_eventSinks, eventSink => { eventSink.WriteTelegram(telegram, cancellationToken); });
            await Task.WhenAll(sinkTasks.ToArray());
        }
    }
}

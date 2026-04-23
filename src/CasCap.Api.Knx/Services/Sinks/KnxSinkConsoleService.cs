namespace CasCap.Services;

/// <inheritdoc/>
[SinkType("Console")]
public class KnxSinkConsoleService(ILogger<KnxSinkConsoleService> logger, IOptions<KnxConfig> config) : IEventSink<KnxEvent>
{
    /// <inheritdoc/>
    public Task WriteEvent(KnxEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@Telegram}", nameof(KnxSinkConsoleService), @event);
        //TODO: merge below into ToString() of KnxTelegram?
        var logLevel = string.IsNullOrEmpty(config.Value.BusLoggingGroupAddressFilter)
            || @event.Kga.Name.Contains(config.Value.BusLoggingGroupAddressFilter, StringComparison.OrdinalIgnoreCase)
            ? LogLevel.Information
            : LogLevel.Trace;
        logger.Log(logLevel,"{ClassName} telegram from '{IndividualAddress}' to '{GroupAddress}' ({GroupAddressName}, '{Value}{ValueLabel}')",
            nameof(KnxSinkConsoleService), @event.Args.SourceAddress, @event.Kga.GroupAddress, @event.Kga.Name, @event.Value,
            string.IsNullOrWhiteSpace(@event.ValueLabel) ? string.Empty : $" ({@event.ValueLabel})");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KnxEvent> GetEvents(string? id = null, int limit = 1000, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(0, cancellationToken);
        throw new NotSupportedException();
        yield return null;
    }
}

namespace CasCap.Services;

/// <inheritdoc/>
[SinkType("Console")]
public partial class KnxSinkConsoleService(ILogger<KnxSinkConsoleService> logger, IOptions<KnxConfig> config) : IEventSink<KnxEvent>
{
    /// <inheritdoc/>
    public string SinkType => "Console";

    /// <inheritdoc/>
    public Task WriteEvent(KnxEvent @event, CancellationToken cancellationToken = default)
    {
        LogWriteEvent(logger, nameof(KnxSinkConsoleService), @event.Kga.Name);
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


    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} processing telegram for {GroupAddressName}")]
    private static partial void LogWriteEvent(ILogger logger, string className, string groupAddressName);
}

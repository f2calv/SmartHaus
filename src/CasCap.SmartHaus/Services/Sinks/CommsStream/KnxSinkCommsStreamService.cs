using System.Runtime.CompilerServices;

namespace CasCap.Services;

/// <summary>
/// Tracks state changes on configured group addresses and writes LLM-friendly alerts
/// to the comms Redis Stream via <see cref="IEventSink{T}"/>.
/// </summary>
/// <remarks>
/// Also subscribes to <see cref="IKnxConnectionNotifier"/> and publishes
/// bus connection dropped/reconnected events to the comms stream.
/// </remarks>
[SinkType("CommsStream")]
public class KnxSinkCommsStreamService : IEventSink<KnxEvent>
{
    private readonly ILogger _logger;
    private readonly KnxConfig _config;
    private readonly IKnxState _knxState;
    private readonly IEventSink<CommsEvent> _commsSink;

    /// <inheritdoc/>
    public KnxSinkCommsStreamService(ILogger<KnxSinkCommsStreamService> logger,
        IOptions<KnxConfig> config,
        IKnxState knxState,
        IEventSink<CommsEvent> commsSink,
        IKnxConnectionNotifier connectionNotifier
        )
    {
        _logger = logger;
        _config = config.Value;
        _knxState = knxState;
        _commsSink = commsSink;
        _ = ProcessConnectionEventsAsync(connectionNotifier)
            .ContinueWith(t => _logger.LogError(t.Exception, "{ClassName} connection event processing faulted",
                nameof(KnxSinkCommsStreamService)), TaskContinuationOptions.OnlyOnFaulted);
    }

    private bool FirstRun = true;

    /// <inheritdoc/>
    public async Task WriteEvent(KnxEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("{ClassName} {@Telegram}", nameof(KnxSinkCommsStreamService), @event);

        if (FirstRun)
        {
            try
            {
                foreach (var groupAddressName in _config.StateChangeAlerts.Keys)
                {
                    var knxState = await _knxState.GetKnxState(groupAddressName, cancellationToken);
                    if (knxState is not null)
                        dStates.TryAdd(groupAddressName, knxState);
                }
                _logger.LogInformation("{ClassName} loaded {Count} state change group addresses for alerting purposes",
                    nameof(KnxSinkCommsStreamService), _config.StateChangeAlerts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ClassName} failed to set initial states for state change alerting", nameof(KnxSinkCommsStreamService));
            }

            FirstRun = false;
        }

        await ProcessStateChangeAlerts(@event, cancellationToken);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KnxEvent> GetEvents(string? id = null, int limit = 1000, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(0, cancellationToken);
        throw new NotSupportedException();
        yield return null;
    }

    /// <summary>
    /// Tracks the last known <see cref="State"/> for each monitored group address.
    /// </summary>
    private static Dictionary<string, State> dStates { get; set; } = new();

    /// <summary>
    /// Compares the incoming telegram value against the last known state and sends an alert if a change is detected.
    /// </summary>
    private async Task ProcessStateChangeAlerts(KnxEvent knxEvent, CancellationToken cancellationToken)
    {
        var newState = new State(knxEvent.Kga.Name, knxEvent.ValueAsString, knxEvent.ValueLabel, knxEvent.TimestampUtc);
        dStates.TryGetValue(knxEvent.Kga.Name, out var oldState);
        if (oldState is not null && oldState != newState)
        {
            var oldValue = oldState.ValueLabel ?? oldState.Value;
            var newValue = newState.ValueLabel ?? newState.Value;
            var friendly = _config.StateChangeAlerts.GetValueOrDefault(knxEvent.Kga.Name, knxEvent.Kga.Name);

            _logger.LogInformation("{ClassName} {FriendlyName} state changed from {OldValue} to {NewValue}",
                nameof(KnxSinkCommsStreamService), friendly, oldValue, newValue);
            var commsEvent = new CommsEvent
            {
                Source = nameof(KnxSinkCommsStreamService),
                Message = $"{friendly} changed from '{oldValue}' to '{newValue}'",
                TimestampUtc = knxEvent.TimestampUtc,
            };
            await _commsSink.WriteEvent(commsEvent, cancellationToken);
        }
        dStates[knxEvent.Kga.Name] = newState;
    }

    /// <summary>
    /// Reads bus connection state changes from <see cref="IKnxConnectionNotifier"/>
    /// and publishes them as <see cref="CommsEvent"/> to the comms stream.
    /// </summary>
    private async Task ProcessConnectionEventsAsync(IKnxConnectionNotifier connectionNotifier)
    {
        try
        {
            await foreach (var change in connectionNotifier.Reader.ReadAllAsync())
            {
                try
                {
                    var action = change.Connected ? "reconnected" : "dropped";
                    var commsEvent = new CommsEvent
                    {
                        Source = nameof(KnxSinkCommsStreamService),
                        Message = $"Home automation bus connection {action}",
                        TimestampUtc = change.TimestampUtc,
                    };
                    await _commsSink.WriteEvent(commsEvent);
                    _logger.LogInformation("{ClassName} published connection {Action} event for {AreaLine}",
                        nameof(KnxSinkCommsStreamService), action, change.AreaLine);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{ClassName} failed to publish connection event for {AreaLine}",
                        nameof(KnxSinkCommsStreamService), change.AreaLine);
                }
            }
        }
        catch (OperationCanceledException)
        {
            //expected during shutdown
        }
    }
}

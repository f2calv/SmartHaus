using System.Collections.Concurrent;

namespace CasCap.Services;

/// <summary>
/// Hub-side event sink that periodically logs the count of <see cref="HubEvent"/> messages
/// received in the most recent log interval.
/// When no new messages arrive for more than <c>SuppressAfterInactivityMinutes</c> minutes
/// the sink emits a single "suppressing logs" notice and goes silent until traffic returns.
/// </summary>
/// <remarks>
/// Reads the following key from <see cref="SinkConfigParams.Settings"/>:
/// <list type="bullet">
///   <item><description>
///     <see cref="SuppressAfterInactivityMinutes"/> — minutes of silence before
///     output is suppressed. Defaults to <c>5</c>.
///   </description></item>
/// </list>
/// </remarks>
[SinkType("Console")]
public class HausHubSinkConsoleService : IEventSink<HubEvent>
{
    private const string SuppressAfterInactivityMinutes = nameof(SuppressAfterInactivityMinutes);

    private readonly ILogger _logger;
    private readonly TimeSpan _suppressAfter;

    // Counts accumulated since the last log output, keyed by EventType
    private readonly ConcurrentDictionary<string, long> _intervalCounts = new();
    private DateTimeOffset _lastEventAt = DateTimeOffset.MinValue;
    private bool _suppressed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HausHubSinkConsoleService"/> class.
    /// </summary>
    public HausHubSinkConsoleService(ILogger<HausHubSinkConsoleService> logger,
        IOptions<SignalRHubConfig> config)
    {
        _logger = logger;

        var sinkParams = config.Value.Sinks.AvailableSinks.GetValueOrDefault("Console");
        var minutes = 5;
        if (sinkParams is not null
            && sinkParams.Settings.TryGetValue(SuppressAfterInactivityMinutes, out var raw)
            && int.TryParse(raw, out var parsed)
            && parsed > 0)
            minutes = parsed;

        _suppressAfter = TimeSpan.FromMinutes(minutes);
        _logInterval = TimeSpan.FromMilliseconds(config.Value.ConsoleLogIntervalMs);
    }

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(() => PeriodicLogLoopAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task WriteEvent(HubEvent @event, CancellationToken cancellationToken = default)
    {
        _intervalCounts.AddOrUpdate(@event.EventType, 1, (_, count) => count + 1);
        _lastEventAt = @event.Timestamp;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<HubEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    #region private helpers

    private readonly TimeSpan _logInterval;

    private async Task PeriodicLogLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_logInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var now = DateTimeOffset.UtcNow;
            var idle = now - _lastEventAt;

            if (idle >= _suppressAfter)
            {
                if (!_suppressed)
                {
                    _suppressed = true;
                    _logger.LogInformation("{ClassName} suppressing logs until new activity",
                        nameof(HausHubSinkConsoleService));
                }
                continue;
            }

            if (_suppressed)
            {
                _suppressed = false;
                _logger.LogInformation("{ClassName} resuming log output — activity detected",
                    nameof(HausHubSinkConsoleService));
            }

            // Snapshot and reset interval counters atomically
            var snapshot = new Dictionary<string, long>();
            foreach (var key in _intervalCounts.Keys)
            {
                var count = _intervalCounts.TryRemove(key, out var c) ? c : 0;
                if (count > 0)
                    snapshot[key] = count;
            }

            var total = snapshot.Values.Sum();
            var perType = string.Join(", ", snapshot
                .OrderBy(kv => kv.Key)
                .Select(kv => $"{kv.Key}={kv.Value}"));

            _logger.LogInformation("{ClassName} {Total} events in last {IntervalSeconds}s ({PerType})",
                nameof(HausHubSinkConsoleService), total, (int)_logInterval.TotalSeconds, perType);
        }
    }

    #endregion
}

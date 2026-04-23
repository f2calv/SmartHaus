using System.Collections.Concurrent;

namespace CasCap.Services;

/// <summary>
/// Monitors compute node events for GPU overtemperature conditions
/// and writes alerts to the comms Redis Stream via <see cref="IEventSink{T}"/>.
/// The alert fires only once per threshold crossing (rising edge) and is debounced
/// via hysteresis (<see cref="EdgeHardwareConfig.GpuAlertHysteresis"/>) and a cooldown
/// period (<see cref="EdgeHardwareConfig.GpuAlertCooldownMs"/>) to prevent alert flooding
/// when GPU temperature oscillates around the threshold.
/// </summary>
[SinkType("CommsStream")]
public class EdgeHardwareSinkCommsStreamService(ILogger<EdgeHardwareSinkCommsStreamService> logger,
    IOptions<EdgeHardwareConfig> config,
    IEventSink<CommsEvent> commsSink) : IEventSink<EdgeHardwareEvent>
{
    private readonly double _gpuAlertThreshold = config.Value.GpuAlertThresholdC;
    private readonly double _gpuRearmThreshold = config.Value.GpuAlertThresholdC - config.Value.GpuAlertHysteresis;
    private readonly TimeSpan _cooldown = TimeSpan.FromMilliseconds(config.Value.GpuAlertCooldownMs);

    private readonly ConcurrentDictionary<string, bool> _alertFiredByNode = [];
    private readonly ConcurrentDictionary<string, DateTime> _lastAlertUtcByNode = [];

    /// <inheritdoc/>
    public async Task WriteEvent(EdgeHardwareEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@EdgeHardwareEvent}", nameof(EdgeHardwareSinkCommsStreamService), @event);

        if (@event.GpuTemperatureC is null)
            return;

        _alertFiredByNode.TryGetValue(@event.NodeName, out var alertFired);
        _lastAlertUtcByNode.TryGetValue(@event.NodeName, out var lastAlertUtc);

        if (@event.GpuTemperatureC >= _gpuAlertThreshold)
        {
            if (!alertFired && @event.TimestampUtc - lastAlertUtc >= _cooldown)
            {
                _alertFiredByNode[@event.NodeName] = true;
                _lastAlertUtcByNode[@event.NodeName] = @event.TimestampUtc;
                logger.LogWarning("{ClassName} GPU overtemp on {NodeName}: {GpuTemp}°C",
                    nameof(EdgeHardwareSinkCommsStreamService), @event.NodeName, @event.GpuTemperatureC);
                await commsSink.WriteEvent(new CommsEvent
                {
                    Source = nameof(EdgeHardwareSinkCommsStreamService),
                    Message = $"Compute node {@event.NodeName} GPU temperature alert: {@event.GpuTemperatureC:F1}°C (threshold {_gpuAlertThreshold}°C)",
                    TimestampUtc = @event.TimestampUtc,
                }, cancellationToken);
            }
        }
        else if (@event.GpuTemperatureC < _gpuRearmThreshold)
            _alertFiredByNode[@event.NodeName] = false;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<EdgeHardwareEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

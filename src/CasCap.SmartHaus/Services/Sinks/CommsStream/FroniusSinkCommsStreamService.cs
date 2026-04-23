namespace CasCap.Services;

/// <summary>
/// Writes a <see cref="CommsEvent"/> to the comms Redis Stream when the home battery
/// charge level exceeds the configured <see cref="FroniusConfig.SocAlertThreshold"/>.
/// The alert fires only once per threshold crossing (rising edge) and is debounced
/// via hysteresis (<see cref="FroniusConfig.SocAlertHysteresis"/>) and a cooldown
/// period (<see cref="FroniusConfig.SocAlertCooldownMs"/>) to prevent alert flooding
/// when the charge level oscillates around the threshold.
/// </summary>
[SinkType("CommsStream")]
public class FroniusSinkCommsStreamService(ILogger<FroniusSinkCommsStreamService> logger, IOptions<FroniusConfig> config, IEventSink<CommsEvent> commsSink) : IEventSink<FroniusEvent>
{
    private readonly double _socAlertThreshold = config.Value.SocAlertThreshold;
    private readonly double _socRearmThreshold = config.Value.SocAlertThreshold - config.Value.SocAlertHysteresis;
    private readonly TimeSpan _cooldown = TimeSpan.FromMilliseconds(config.Value.SocAlertCooldownMs);

    private bool _alertFired;
    private DateTime _lastAlertUtc = DateTime.MinValue;

    /// <inheritdoc/>
    public async Task WriteEvent(FroniusEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event.SOC >= _socAlertThreshold)
        {
            if (!_alertFired && @event.TimestampUtc - _lastAlertUtc >= _cooldown)
            {
                _alertFired = true;
                _lastAlertUtc = @event.TimestampUtc;
                var commsEvent = new CommsEvent
                {
                    Source = nameof(FroniusSinkCommsStreamService),
                    Message = $"Home battery charge reached {@event.SOC:P0} (threshold {_socAlertThreshold:P0}), "
                        + $"Solar={@event.P_PV:F0}W, Grid={@event.P_Grid:F0}W, Load={@event.P_Load:F0}W",
                    TimestampUtc = @event.TimestampUtc,
                };
                await commsSink.WriteEvent(commsEvent, cancellationToken);
                logger.LogInformation("{ClassName} SOC alert fired at {Soc}",
                    nameof(FroniusSinkCommsStreamService), @event.SOC);
            }
        }
        else if (@event.SOC < _socRearmThreshold)
            _alertFired = false;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<FroniusEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}

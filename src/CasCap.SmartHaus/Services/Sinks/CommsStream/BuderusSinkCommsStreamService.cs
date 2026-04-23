namespace CasCap.Services;

/// <summary>
/// Writes a <see cref="CommsEvent"/> to the comms Redis Stream when the hot water
/// temperature reaches the configured target. The alert fires only once per threshold
/// crossing (rising edge) and is debounced via hysteresis
/// (<see cref="HeatingAgentConfig.Dhw1AlertHysteresis"/>) and a cooldown period
/// (<see cref="HeatingAgentConfig.Dhw1AlertCooldownMs"/>) to prevent alert flooding when
/// the temperature oscillates around the target.
/// </summary>
/// <remarks>
/// The intent is for the CommsAgent to evaluate whether to temporarily raise the
/// hot water target when the solar battery is full and there is surplus solar production —
/// effectively using the hot water tank as a thermal battery.
/// </remarks>
[SinkType("CommsStream")]
public class BuderusSinkCommsStreamService(ILogger<BuderusSinkCommsStreamService> logger, IOptions<HeatingAgentConfig> heatingAgentConfig, IEventSink<CommsEvent> commsSink) : IEventSink<BuderusEvent>
{
    private readonly double _hysteresis = heatingAgentConfig.Value.Dhw1AlertHysteresis;
    private readonly TimeSpan _cooldown = TimeSpan.FromMilliseconds(heatingAgentConfig.Value.Dhw1AlertCooldownMs);

    private double? _currentSetpoint;
    private double? _lastActualTemp;
    private bool _alertFired;
    private DateTime _lastAlertUtc = DateTime.MinValue;

    /// <inheritdoc/>
    public async Task WriteEvent(BuderusEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event.type is BuderusGauges.dhwCircuits_dhw1_currentSetpoint
            && double.TryParse(@event.Value, out var setpoint))
        {
            _currentSetpoint = setpoint;

            //re-evaluate alert state in case the setpoint changed while above threshold
            if (_lastActualTemp is not null)
                await EvaluateAlert(_lastActualTemp.Value, @event.TimestampUtc, cancellationToken);
            return;
        }

        if (@event.type is not BuderusGauges.dhwCircuits_dhw1_actualTemp)
            return;

        if (!double.TryParse(@event.Value, out var actualTemp))
            return;

        _lastActualTemp = actualTemp;
        if (_currentSetpoint is not null)
            await EvaluateAlert(actualTemp, @event.TimestampUtc, cancellationToken);
    }

    /// <summary>
    /// Checks whether the actual temperature meets or exceeds the current target and fires
    /// an alert (once per crossing) respecting hysteresis and cooldown.
    /// </summary>
    private async Task EvaluateAlert(double actualTemp, DateTime timestampUtc, CancellationToken cancellationToken)
    {
        if (_currentSetpoint is null)
            return;

        if (actualTemp >= _currentSetpoint.Value)
        {
            if (!_alertFired && timestampUtc - _lastAlertUtc >= _cooldown)
            {
                _alertFired = true;
                _lastAlertUtc = timestampUtc;
                var commsEvent = new CommsEvent
                {
                    Source = nameof(BuderusSinkCommsStreamService),
                    Message = $"Hot water temperature ({actualTemp:F1}°C) has reached the target ({_currentSetpoint.Value:F1}°C). "
                        + "Consider raising the target if the solar battery is full and there is surplus solar production.",
                    TimestampUtc = timestampUtc,
                };
                await commsSink.WriteEvent(commsEvent, cancellationToken);
                logger.LogInformation("{ClassName} DHW1 setpoint alert fired at {ActualTemp}°C (setpoint {Setpoint}°C)",
                    nameof(BuderusSinkCommsStreamService), actualTemp, _currentSetpoint.Value);
            }
        }
        else if (actualTemp < _currentSetpoint.Value - _hysteresis)
            _alertFired = false;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<BuderusEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}

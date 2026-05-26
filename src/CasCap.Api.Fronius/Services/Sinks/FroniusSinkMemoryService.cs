namespace CasCap.Services;

/// <summary>
/// In-memory event sink that stores the latest <see cref="FroniusEvent"/> and provides
/// snapshot queries without requiring external infrastructure.
/// </summary>
[SinkType("Memory")]
public partial class FroniusSinkMemoryService(ILogger<FroniusSinkMemoryService> logger) : IEventSink<FroniusEvent>, IFroniusQuery
{
    private FroniusEvent? _latest;

    /// <inheritdoc/>
    public Task WriteEvent(FroniusEvent @event, CancellationToken cancellationToken = default)
    {
        LogWriteEvent(logger, nameof(FroniusSinkMemoryService));
        _latest = @event;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<InverterSnapshot> GetSnapshot()
    {
        if (_latest is null)
            return Task.FromResult(new InverterSnapshot());

        return Task.FromResult(new InverterSnapshot
        {
            StateOfCharge = _latest.SOC,
            BatteryPower = _latest.P_Akku,
            GridPower = _latest.P_Grid,
            LoadPower = _latest.P_Load,
            PhotovoltaicPower = _latest.P_PV,
            ReadingUtc = new DateTimeOffset(_latest.TimestampUtc, TimeSpan.Zero),
        });
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<FroniusEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        if (_latest is not null)
            yield return _latest;
    }

    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} processing inverter event")]
    private static partial void LogWriteEvent(ILogger logger, string className);
}

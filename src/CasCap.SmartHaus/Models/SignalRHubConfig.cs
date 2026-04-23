namespace CasCap.Models;

/// <summary>
/// Configuration options for the consolidated SignalR hub feature (<c>SignalRHub</c>).
/// Hub-client pods (Fronius, KNX, DoorBird, Buderus) that connect to the hub as SignalR clients
/// also read <see cref="HubPath"/> from this section so they know where to connect.
/// </summary>
public record SignalRHubConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(SignalRHubConfig)}";

    /// <summary>
    /// Base address of the SignalR hub server that sink clients connect to.
    /// Used by <see cref="CasCap.Services.HausSignalRSinkBase{T}"/> and its derived sinks.
    /// </summary>
    [Required]
    public required Uri SignalRHub { get; init; }

    /// <summary>
    /// The URL path at which the SignalR hub is mounted. Defaults to <c>"/hubs/haus"</c>.
    /// </summary>
    public string HubPath { get; init; } = "/hubs/haus";

    /// <summary>
    /// Configuration for event data sinks registered in the hub server process.
    /// Defaults to <c>Console</c> and <c>Metrics</c> enabled when not explicitly configured.
    /// </summary>
    [ValidateObjectMembers]
    public SinkConfig Sinks { get; init; } = new()
    {
        AvailableSinks = new Dictionary<string, SinkConfigParams>
        {
            ["Console"] = new() { Enabled = true },
            ["Metrics"] = new() { Enabled = true },
        }
    };

    /// <summary>
    /// Logging interval in milliseconds for the console sink periodic event count output.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>30000</c> ms (30 seconds).
    /// Used by <see cref="CasCap.Services.HausHubSinkConsoleService"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int ConsoleLogIntervalMs { get; init; } = 30_000;

    /// <summary>
    /// Number of events to accumulate in the metrics sink before flushing to the OpenTelemetry counter.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>10</c>.
    /// Used by <see cref="CasCap.Services.HausHubSinkMetricsService"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int MetricsBatchSize { get; init; } = 10;

    /// <summary>
    /// Periodic flush interval in milliseconds for the metrics sink.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>60000</c> ms (60 seconds).
    /// Used by <see cref="CasCap.Services.HausHubSinkMetricsService"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int MetricsFlushIntervalMs { get; init; } = 60_000;
}

using Knx.Falcon.KnxnetIp;

namespace CasCap.Models;

/// <summary>KNX home automation configuration.</summary>
public record KnxConfig : IAppConfig, IAzTableStorageConfig, IMetricsConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(KnxConfig)}";

    /// <summary>The KNX service family to connect with.</summary>
    /// <remarks>
    /// Must be explicitly configured in <c>appsettings.json</c>.
    /// Supported values: <see cref="Knx.Falcon.KnxnetIp.ServiceFamily.Tunneling"/> (UDP point-to-point)
    /// and <see cref="Knx.Falcon.KnxnetIp.ServiceFamily.Routing"/> (UDP multicast).
    /// When <see cref="Knx.Falcon.KnxnetIp.ServiceFamily.Routing"/> is selected,
    /// <see cref="ShardingMode"/> is forced to <see cref="Models.ShardingMode.Unified"/>.
    /// </remarks>
    [Required]
    public required ServiceFamily ServiceFamily { get; init; }

    /// <inheritdoc cref="Models.ShardingMode"/>
    /// <remarks>
    /// Only <see cref="Models.ShardingMode.Unified"/> is currently supported.
    /// <see cref="Models.ShardingMode.Partitioned"/> will be available in a future release
    /// and is only valid when <see cref="ServiceFamily"/> is
    /// <see cref="Knx.Falcon.KnxnetIp.ServiceFamily.Tunneling"/>.
    /// Defaults to <see cref="Models.ShardingMode.Unified"/>.
    /// </remarks>
    [Required]
    public required ShardingMode ShardingMode { get; init; } = ShardingMode.Unified;

    /// <inheritdoc cref="KubernetesProbeTypes"/>
    [Required]
    public required KubernetesProbeTypes HealthCheck { get; init; } = KubernetesProbeTypes.Readiness;

    /// <summary>Area/line filter for tunneling connections.</summary>
    /// <remarks>
    /// Only applies when <see cref="ServiceFamily"/> is
    /// <see cref="Knx.Falcon.KnxnetIp.ServiceFamily.Tunneling"/>.
    /// Must be configured in <c>appsettings.json</c>. Example: <c>["1.1", "1.2", "1.3"]</c>.
    /// </remarks>
    [Required]
    public required string[] TunnelingAreaLineFilter { get; init; } = [];

    /// <summary>Group address names that trigger state change alerts, mapped to LLM-friendly descriptions.</summary>
    /// <remarks>Must be configured in <c>appsettings.json</c> with site-specific alert addresses. The key is the group address name and the value is the friendly description used in comms stream messages.</remarks>
    [Required]
    public required Dictionary<string, string> StateChangeAlerts { get; init; } = [];

    /// <summary>File path to the KNX group address XML export file.</summary>
    /// <remarks>
    /// Must be configured in <c>appsettings.json</c>.
    /// In Development mode, relative paths are combined with <see cref="AppDomain.BaseDirectory"/>; absolute paths are used as-is.
    /// In Production/Kubernetes, used as an absolute path (e.g. <c>"/etc/knx/knxgroupaddresses.xml"</c>).
    /// </remarks>
    [Required]
    public required string GroupAddressXmlFilePath { get; init; }

    #region DayNight
    /// <summary>Whether the day/night detection feature is enabled.</summary>
    /// <remarks>Defaults to <see langword="true"/>.</remarks>
    [Required]
    public required bool DayNightEnabled { get; init; } = true;

    /// <summary>IANA time zone location for sunrise/sunset calculations.</summary>
    /// <remarks>Must be configured in <c>appsettings.json</c> (e.g. <c>"Berlin"</c>, <c>"London"</c>).</remarks>
    [Required]
    public required string DayNightTimeZoneLocation { get; init; } = default!;

    /// <summary>Latitude for sunrise/sunset calculations.</summary>
    /// <remarks>Must be configured in <c>appsettings.json</c> (e.g. <c>52.520008</c> for Berlin).</remarks>
    [Required, Range(-90.0, 90.0)]
    public required double DayNightLatitude { get; init; }

    /// <summary>Longitude for sunrise/sunset calculations.</summary>
    /// <remarks>Must be configured in <c>appsettings.json</c> (e.g. <c>13.404954</c> for Berlin).</remarks>
    [Required, Range(-180.0, 180.0)]
    public required double DayNightLongitude { get; init; }

    /// <summary>The group address name for the day/night state.</summary>
    /// <remarks>Defaults to <c>"SYS-[Night_Day]"</c>.</remarks>
    [Required]
    public required string DayNightGroupAddressName { get; init; } = "SYS-[Night_Day]";

    /// <summary>Additional group addresses that receive the day/night state alongside <see cref="DayNightGroupAddressName"/>.</summary>
    /// <remarks>
    /// Useful for per-room day/night overrides (e.g. a bedroom GA that delays sunrise).
    /// Must be configured in <c>appsettings.json</c>.
    /// </remarks>
    public string[] DayNightAdditionalGroupAddresses { get; init; } = [];

    /// <summary>Delay in milliseconds between day/night state pushes to the KNX bus.</summary>
    /// <remarks>Used by <see cref="CasCap.Services.KnxAutomationBgService"/>. Defaults to <c>300000</c> ms (5 minutes). The first push occurs immediately on startup.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int DayNightPollingDelayMs { get; init; } = 300_000;
    #endregion

    /// <summary>Hour override for weekday sunrise (forces day mode at this hour regardless of actual sunrise).</summary>
    /// <remarks>Defaults to <c>6</c>.</remarks>
    [Required, Range(0, 23)]
    public required int DayNightSunriseHourOverrideWeekday { get; init; } = 6;

    /// <summary>Hour override for weekend sunrise (forces day mode at this hour regardless of actual sunrise).</summary>
    /// <remarks>Defaults to <c>7</c>.</remarks>
    [Required, Range(0, 23)]
    public required int DayNightSunriseHourOverrideWeekend { get; init; } = 7;

    /// <summary>Whether verbose logging is enabled for the bus sender.</summary>
    /// <remarks>Defaults to <see langword="true"/>.</remarks>
    [Required]
    public required bool BusSenderLoggingEnabled { get; init; } = true;

    /// <summary>Optional filter for bus logging by group address name substring.</summary>
    /// <remarks>
    /// When set, only matching telegrams are logged at <see cref="Microsoft.Extensions.Logging.LogLevel.Information"/>;
    /// all others at <see cref="Microsoft.Extensions.Logging.LogLevel.Trace"/>.
    /// When <see langword="null"/> or empty, all telegrams are logged at their default level.
    /// </remarks>
    public string? BusLoggingGroupAddressFilter { get; init; }

    /// <summary>Delay in milliseconds between each poll when waiting for a KNX state change after sending a value.</summary>
    /// <remarks>Used by <see cref="CasCap.Services.KnxQueryService"/>. Defaults to <c>100</c> ms.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int StateChangePollingDelayMs { get; init; } = 100;

    /// <summary>Maximum consecutive stale poll iterations before a state change attempt is considered unsuccessful.</summary>
    /// <remarks>Defaults to <c>20</c>.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int StateChangeMaxPollIterations { get; init; } = 20;

    /// <summary>Delay in milliseconds between each poll of the <see cref="CasCap.Abstractions.IStateChangeQueue"/> when empty.</summary>
    /// <remarks>Used by <see cref="CasCap.Services.KnxAutomationBgService"/>. Defaults to <c>50</c> ms.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int QueuePollingDelayMs { get; init; } = 50;

    /// <summary>Maximum concurrent <see cref="KnxStateChangeItem"/> entries processed by the queue.</summary>
    /// <remarks>
    /// Used by <see cref="CasCap.Services.KnxAutomationBgService"/>. Controls how many items
    /// are processed in parallel to avoid large backlogs. Defaults to <c>5</c>.
    /// </remarks>
    [Required, Range(1, int.MaxValue)]
    public required int QueueMaxConcurrency { get; init; } = 5;

    /// <summary>Delay in milliseconds between KNX IP device discovery retry attempts.</summary>
    /// <remarks>Used by <see cref="CasCap.Services.KnxMonitorBgService"/>. Defaults to <c>10000</c> ms.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int DiscoveryRetryDelayMs { get; init; } = 10_000;

    /// <summary>Delay in milliseconds between each poll when waiting for the KNX connection at startup.</summary>
    /// <remarks>
    /// Used by <see cref="CasCap.Services.KnxAutomationBgService"/> and
    /// <see cref="CasCap.Services.KnxMonitorBgService"/>. Defaults to <c>1000</c> ms.
    /// </remarks>
    [Required, Range(1, int.MaxValue)]
    public required int ConnectionPollingDelayMs { get; init; } = 1_000;

    /// <summary>Number of connection polling attempts between each log-level escalation.</summary>
    /// <remarks>
    /// Escalates from <see cref="Microsoft.Extensions.Logging.LogLevel.Trace"/> to
    /// <see cref="Microsoft.Extensions.Logging.LogLevel.Warning"/>. Used by
    /// <see cref="CasCap.Services.KnxAutomationBgService"/> and
    /// <see cref="CasCap.Services.KnxMonitorBgService"/>. Defaults to <c>10</c>.
    /// </remarks>
    [Required, Range(1, int.MaxValue)]
    public required int ConnectionLogEscalationInterval { get; init; } = 10;

    /// <summary>Delay in milliseconds between each health poll of active KNX bus connections.</summary>
    /// <remarks>Used by <see cref="CasCap.Services.KnxMonitorBgService"/>. Defaults to <c>1000</c> ms.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int ConnectionHealthPollingDelayMs { get; init; } = 1_000;

    /// <summary>Initial delay in milliseconds before the first reconnection attempt after a connection drop.</summary>
    /// <remarks>
    /// Doubles after each failed attempt (exponential back-off) up to <see cref="ReconnectMaxBackoffMs"/>.
    /// Used by <see cref="CasCap.Services.KnxMonitorBgService"/>. Defaults to <c>1000</c> ms.
    /// </remarks>
    [Required, Range(1, int.MaxValue)]
    public required int ReconnectBackoffMs { get; init; } = 1_000;

    /// <summary>Maximum delay in milliseconds between reconnection attempts.</summary>
    /// <remarks>
    /// Caps the exponential back-off that starts at <see cref="ReconnectBackoffMs"/>.
    /// Used by <see cref="CasCap.Services.KnxMonitorBgService"/>. Defaults to <c>60000</c> ms.
    /// </remarks>
    [Required, Range(1, int.MaxValue)]
    public required int ReconnectMaxBackoffMs { get; init; } = 60_000;

    /// <inheritdoc/>
    [Required]
    public required string AzureTableStorageConnectionString { get; init; }

    /// <inheritdoc/>
    /// <remarks>Defaults to <see cref="KubernetesProbeTypes.Startup"/>.</remarks>
    [Required]
    public required KubernetesProbeTypes HealthCheckAzureTableStorage { get; init; } = KubernetesProbeTypes.None;

    /// <summary>Configuration for event data sinks.</summary>
    [Required, ValidateObjectMembers]
    public required SinkConfig Sinks { get; init; } = default!;

    /// <summary>Expiry in days applied to Redis series sorted set keys.</summary>
    /// <remarks>Defaults to <c>7</c>.</remarks>
    [Range(1, int.MaxValue)]
    public int RedisSeriesExpiryDays { get; init; } = 7;

    /// <summary>Transport used to broker KNX telegrams between producer and consumer services.</summary>
    /// <remarks>
    /// Defaults to <see cref="TelegramBrokerMode.Redis"/> which is required for Kubernetes
    /// deployments (any replica count) and for external access from MCP tools / Agents.
    /// Use <see cref="TelegramBrokerMode.Channel"/> only during local development where all
    /// services run in the same process and no external callers need to send commands.
    /// </remarks>
    [Required]
    public required TelegramBrokerMode TelegramBrokerMode { get; init; } = TelegramBrokerMode.Redis;

    /// <summary>Redis stream key used for incoming telegrams.</summary>
    /// <remarks>Active when <see cref="TelegramBrokerMode"/> is <see cref="TelegramBrokerMode.Redis"/>.</remarks>
    [Required]
    public required string TelegramStreamKeyIncoming { get; init; } = "knx:stream:incoming";

    /// <summary>Redis stream key used for outgoing telegrams.</summary>
    /// <remarks>Active when <see cref="TelegramBrokerMode"/> is <see cref="TelegramBrokerMode.Redis"/>.</remarks>
    [Required]
    public required string TelegramStreamKeyOutgoing { get; init; } = "knx:stream:outgoing";

    /// <summary>Redis consumer group name used by competing consumers.</summary>
    /// <remarks>Active when <see cref="TelegramBrokerMode"/> is <see cref="TelegramBrokerMode.Redis"/>.</remarks>
    [Required]
    public required string TelegramConsumerGroup { get; init; } = "knx:processors";

    /// <summary>Starting ID used when creating the consumer group for the first time.</summary>
    /// <remarks>Defaults to <c>"0"</c> (read from the beginning). Used by <see cref="CasCap.Services.RedisKnxTelegramBroker{T}"/>.</remarks>
    [Required]
    public required string TelegramConsumerGroupStartId { get; init; } = "0";

    /// <summary>Maximum entries to read from the Redis stream per <c>XREADGROUP</c> call.</summary>
    /// <remarks>Used by <see cref="CasCap.Services.RedisKnxTelegramBroker{T}"/>. Defaults to <c>10</c>.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int TelegramStreamReadCount { get; init; } = 10;

    /// <summary>Redis stream read position passed to <c>XREADGROUP</c>.</summary>
    /// <remarks>Used by <see cref="CasCap.Services.RedisKnxTelegramBroker{T}"/>. Defaults to <c>"&gt;"</c> (new messages only).</remarks>
    [Required]
    public required string TelegramStreamReadPosition { get; init; } = ">";

    /// <summary>Delay in milliseconds between Redis stream polls when no messages are available.</summary>
    /// <remarks>Used by <see cref="CasCap.Services.RedisKnxTelegramBroker{T}"/>. Defaults to <c>100</c> ms.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int TelegramStreamPollingDelayMs { get; init; } = 100;

    /// <summary>Expiry in days applied to date-partitioned Redis stream keys.</summary>
    /// <remarks>Defaults to <c>7</c>.</remarks>
    [Range(1, int.MaxValue)]
    public int TelegramStreamExpiryDays { get; init; } = 7;

    /// <summary>Optional translation dictionaries keyed by language code (e.g. <c>de</c>).</summary>
    /// <remarks>
    /// Used to generate a glossary block that is injected into the LLM/Agent system
    /// prompt so that end users can query in their native language while the code uses English.
    /// </remarks>
    public Dictionary<string, KnxTranslationLanguage> Translations { get; init; } = [];

    /// <summary>Whether this instance runs in lite mode (no background services).</summary>
    /// <remarks>
    /// Set automatically by <see cref="CasCap.Extensions.ServiceCollectionExtensions.AddKnx"/>.
    /// When <see langword="true"/>, <see cref="CasCap.Services.KnxQueryService"/> sends commands
    /// directly to the outgoing broker instead of enqueuing to <see cref="CasCap.Abstractions.IStateChangeQueue"/>.
    /// Defaults to <see langword="false"/>.
    /// </remarks>
    public bool LiteMode { get; set; }

    /// <inheritdoc/>
    /// <remarks>Defaults to <c>"haus"</c>.</remarks>
    [Required]
    public required string MetricNamePrefix { get; init; } = "haus";

    /// <inheritdoc/>
    /// <remarks>Defaults to <c>"CasCap.App"</c>.</remarks>
    [Required]
    public required string OtelServiceName { get; init; } = "CasCap.App";
}

using Serilog;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace CasCap.Extensions;

/// <summary>
/// Monitoring and observability configuration for the application.
/// </summary>
public static class MonitoringExtensions
{
    /// <inheritdoc cref="SerilogExtensions.GetBootstrapLogger"/>
    public static void GetBootstrapLogger() => SerilogExtensions.GetBootstrapLogger();

    /// <inheritdoc cref="SerilogWebApplicationBuilderExtensions.InitializeSerilog"/>
    public static Microsoft.Extensions.Logging.ILogger InitializeSerilog(this WebApplicationBuilder builder)
        => SerilogWebApplicationBuilderExtensions.InitializeSerilog(builder);

    /// <summary>Initializes OpenTelemetry services for metrics, traces, and logs.</summary>
    /// <param name="builder">Web application builder.</param>
    /// <param name="appConfig">Application configuration.</param>
    /// <param name="connectionMultiplexer">Redis connection multiplexer.</param>
    /// <param name="gitMetadata">Git metadata for resource attributes.</param>
    public static void InitializeOpenTelemetry(this WebApplicationBuilder builder, AppConfig appConfig,
        IConnectionMultiplexer connectionMultiplexer, GitMetadata gitMetadata)
    {
        builder.InitializeOpenTelemetry(
            (IMetricsConfig)appConfig,
            gitMetadata,
            connectionMultiplexer,
            configureMetrics: metricsBuilder =>
            {
                metricsBuilder.AddView($"{appConfig.MetricNamePrefix}.test_processing.time", new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [5, 10, 15, 20]
                });
            },
            configureTracing: tracingBuilder =>
            {
                tracingBuilder.AddSource(AgentExtensions.GetAISourceName(appConfig.MetricNamePrefix));
            });
    }
}

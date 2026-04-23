using Serilog;
using StackExchange.Redis;

namespace CasCap.Extensions;

/// <summary>
/// Monitoring and observability configuration for the application.
/// </summary>
public static class MonitoringExtensions
{
    private static readonly object _lock = new();

    private static bool _mainLoggingInitialized;

    /// <inheritdoc cref="SerilogExtensions.GetBootstrapLogger"/>
    public static void GetBootstrapLogger() => SerilogExtensions.GetBootstrapLogger();

    /// <summary>
    /// Configures Serilog via <see cref="SerilogExtensions.AddCasCapDefaults"/> for console/file logging.
    /// </summary>
    /// <remarks>
    /// Log export to the OTEL collector is now handled by the native OpenTelemetry log exporter
    /// registered in <see cref="InitializeOpenTelemetry"/> via <c>WithLogging</c>, replacing the
    /// previous <c>Serilog.Sinks.OpenTelemetry</c> sink so the entire monitoring stack uses
    /// consistent OTEL libraries for metrics, traces and logs.
    /// </remarks>
    public static Microsoft.Extensions.Logging.ILogger InitializeSerilog(this WebApplicationBuilder builder)
    {
        lock (_lock)
        {
            if (_mainLoggingInitialized)
                return ApplicationLogging.CreateLogger(nameof(Program));

            builder.Host.UseSerilog((hostContext, loggerConfiguration) =>
            {
                loggerConfiguration.AddCasCapDefaults(hostContext.Configuration);
            });

            _mainLoggingInitialized = true;
        }

        return ApplicationLogging.CreateLogger(nameof(Program));
    }

    /// <summary>Initializes OpenTelemetry services for metrics, traces, and logs.</summary>
    /// <param name="builder">Web application builder.</param>
    /// <param name="appConfig">Application configuration.</param>
    /// <param name="connectionStrings">Connection strings configuration.</param>
    /// <param name="connectionMultiplexer">Redis connection multiplexer.</param>
    /// <param name="gitMetadata">Git metadata for resource attributes.</param>
    public static void InitializeOpenTelemetry(this WebApplicationBuilder builder, AppConfig appConfig, ConnectionStrings connectionStrings,
        IConnectionMultiplexer connectionMultiplexer, GitMetadata gitMetadata)
    {
        if (connectionStrings.OtlpExporter is null || connectionStrings.OtlpExporter == default)
        {
            Serilog.Log.Warning("OtlpExporter is null/empty so skipping registration");
            return;
        }
        var attributes = new Dictionary<string, object>
            {
                { "service.version", gitMetadata.GIT_TAG },
                { "deployment.environment", builder.Environment.EnvironmentName }
            };
        var resourceBuilder = ResourceBuilder.CreateDefault().AddService(
            serviceName: appConfig.OtelServiceName,
            serviceInstanceId: Environment.MachineName
            ).AddAttributes(attributes);

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metricsBuilder =>
            {
                metricsBuilder.SetResourceBuilder(resourceBuilder).AddMeter(appConfig.MetricNamePrefix);
                if (builder.Environment.IsDevelopment())
                {
                    //metricsBuilder.AddConsoleExporter();//only for adhoc debugging
                    metricsBuilder.AddPrometheusExporter();//for local debugging of metrics
                }
                else
                {
                    // ASP.NET metrics: requests, errors, etc...
                    metricsBuilder.AddAspNetCoreInstrumentation(
                    /*
                    o =>
                    {
                        o.Filter = (_, ctx) => ctx.Request.Path != "/metrics";
                    }
                    */
                    );
                    // Runtime metrics: GC, threads, etc...
                    metricsBuilder.AddRuntimeInstrumentation();

                    // resource utilization metrics: CPU, RAM, etc...
                    metricsBuilder.AddProcessInstrumentation();
                }
                metricsBuilder.AddView($"{appConfig.MetricNamePrefix}.test_processing.time", new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [5, 10, 15, 20]
                });
                metricsBuilder.AddOtlpExporter(opt =>
                {
                    opt.Protocol = OtlpExportProtocol.Grpc;
                    opt.Endpoint = connectionStrings.OtlpExporter;
                });

            })
            .WithTracing(tracingBuilder =>
            {
                tracingBuilder.SetResourceBuilder(resourceBuilder);
                // Capture AI agent chat-completion and function-calling spans.
                tracingBuilder.AddSource(AgentExtensions.GetAISourceName(appConfig.MetricNamePrefix));
                if (!builder.Environment.IsDevelopment())
                    tracingBuilder.AddAspNetCoreInstrumentation(o =>
                    {
                        o.Filter = context =>
                        {
                            if (context.Request.Path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase))
                                return false;
                            if (context.Request.Path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase))
                                return false;
                            return true;
                        };
                    });
                // Capture outbound HTTP calls (e.g. Ollama / OpenAI provider requests).
                tracingBuilder.AddHttpClientInstrumentation();
                if (!builder.Environment.IsDevelopment())
                    tracingBuilder.AddRedisInstrumentation(connectionMultiplexer, configure =>
                    {
                        //configure.EnrichActivityWithTimingEvents = false;
                        //configure.SetVerboseDatabaseStatements = true;
                    });
                tracingBuilder.AddOtlpExporter(opt =>
                {
                    opt.Protocol = OtlpExportProtocol.Grpc;
                    opt.Endpoint = connectionStrings.OtlpExporter;
                });
            })
            .WithLogging(loggingBuilder =>
            {
                loggingBuilder.SetResourceBuilder(resourceBuilder);
                loggingBuilder.AddOtlpExporter(opt =>
                {
                    opt.Protocol = OtlpExportProtocol.Grpc;
                    opt.Endpoint = connectionStrings.OtlpExporter;
                });
            });
    }
}

using Serilog;
using StackExchange.Redis;

namespace CasCap;

/// <summary>Builds and runs the SmartHaus server host.</summary>
public static partial class AppHost
{
    /// <summary>Bootstraps and runs the server host.</summary>
    /// <param name="args">Command-line arguments forwarded from the entry point.</param>
    /// <param name="entryAssembly">Entry assembly used for configuration and user-secrets resolution.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args, Assembly entryAssembly)
    {
        SerilogExtensions.GetBootstrapLogger();

        var result = 0;
        try
        {
            // Host builder
            var builder = WebApplication.CreateBuilder(args);

            // Configuration
            var (appConfig, aiConfig, apiAuthConfig, enabledFeatures, gitMetadata) =
                builder.InitializeConfiguration(entryAssembly);

            // Logging
            var logger = SerilogWebApplicationBuilderExtensions.InitializeSerilog(builder);

            // Infrastructure
            var connectionMultiplexer = builder.Services.AddCasCapCaching(builder.Configuration)
                ?? throw new GenericException($"Failed to create {nameof(IConnectionMultiplexer)}");

            // Observability
            builder.InitializeOpenTelemetry(
                (IMetricsConfig)appConfig,
                gitMetadata,
                connectionMultiplexer,
                apiAuthConfig,
                configureMetrics: metricsBuilder => metricsBuilder
                    .AddHistogramView(appConfig.MetricNamePrefix, "test_processing.time", 5, 10, 15, 20),
                configureTracing: tracingBuilder =>
                {
                    tracingBuilder.AddSource(AgentExtensions.GetAISourceName(appConfig.MetricNamePrefix));
                });

            // Feature validation and startup diagnostics
            if (enabledFeatures.Count == 0)
                throw new GenericException(
                    $"{nameof(enabledFeatures)} is not set via Configuration (i.e. appsettings.json or ENV variable)");

            logger.LogInformation("{ClassName} {AppName} running on {NodeName} with features {@Flags}",
                nameof(Program),
                appConfig.PodName ?? AppDomain.CurrentDomain.FriendlyName,
                appConfig.NodeName ?? Environment.MachineName,
                enabledFeatures);

            // Feature registration
            var signalRHubConfig = AddFeatures(
                builder,
                appConfig,
                enabledFeatures,
                connectionMultiplexer);

            // Web API registration
            AddWebApi(builder, enabledFeatures);

            // Build
            var app = builder.Build();

            logger.LogInformation("{ClassName} starting", nameof(Program));

            // Endpoint mapping
            MapEndpoints(app, appConfig, enabledFeatures, signalRHubConfig);

            // Run
            await app.RunAsync();
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not TaskCanceledException)
        {
            result = 1;
            Log.Fatal(exception, "{AppName} terminated unexpectedly", AppDomain.CurrentDomain.FriendlyName);
        }
        catch (Exception exception)
        {
            result = 1;
            Log.Fatal(exception, "Unhandled exception");
        }
        finally
        {
            Log.Information("Stopped {AppName}", AppDomain.CurrentDomain.FriendlyName);
            await Log.CloseAndFlushAsync();
        }

        return result;
    }
}

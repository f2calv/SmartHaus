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
            var builder = WebApplication.CreateBuilder(args);
            var (appConfig, aiConfig, apiAuthConfig, enabledFeatures, gitMetadata) =
                builder.InitializeConfiguration(entryAssembly);
            var logger = SerilogWebApplicationBuilderExtensions.InitializeSerilog(builder);
            var connectionMultiplexer = builder.Services.AddCasCapCaching(builder.Configuration)
                ?? throw new GenericException($"Failed to create {nameof(IConnectionMultiplexer)}");
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

            if (enabledFeatures.Count == 0)
                throw new GenericException(
                    $"{nameof(enabledFeatures)} is not set via Configuration (i.e. appsettings.json or ENV variable)");

            logger.LogInformation("{ClassName} {AppName} running on {NodeName} with features {@Flags}",
                nameof(Program),
                appConfig.PodName ?? AppDomain.CurrentDomain.FriendlyName,
                appConfig.NodeName ?? Environment.MachineName,
                enabledFeatures);

            var signalRHubConfig = AddFeatures(
                builder,
                appConfig,
                aiConfig,
                enabledFeatures,
                connectionMultiplexer);
            AddWebApi(builder, enabledFeatures);

            var app = builder.Build();

            logger.LogInformation("{ClassName} starting", nameof(Program));
            MapEndpoints(app, appConfig, aiConfig, enabledFeatures, signalRHubConfig);

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
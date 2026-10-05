using System.Reflection;

namespace CasCap.App.Console;

/// <summary>Builds and runs the SmartHaus interactive console host.</summary>
public static partial class AppHost
{
    /// <summary>Bootstraps and runs the console host.</summary>
    /// <param name="entryAssembly">Entry assembly used for configuration and user-secrets resolution.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(Assembly entryAssembly)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .MinimumLevel.Override("CasCap", LogEventLevel.Information)
            .WriteTo.Console(outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // Set the static LoggerFactory early so static loggers can resolve during service registration.
        ApplicationLogging.LoggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(Log.Logger);

        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(Log.Logger);
            var (_, _, _, enabledFeatures, _) = builder.InitializeConfiguration(entryAssembly);

            if (enabledFeatures.Count == 0)
                throw new InvalidOperationException(
                    $"{nameof(enabledFeatures)} is empty - set CasCap:FeatureConfig:EnabledFeatures in appsettings or environment variables.");

            AddFeatures(builder, enabledFeatures);

            using var host = builder.Build();
            using var cancellationTokenSource = new CancellationTokenSource();
            System.Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationTokenSource.Cancel();
            };

            host.Services.AddStaticLogging();

            var app = host.Services.GetRequiredService<ConsoleApp>();
            await app.RunAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation via Ctrl+C.
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "Unhandled exception");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }

        return 0;
    }
}
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
            builder.InitializeConfiguration(entryAssembly);
            builder.Services.AddCasCapConfiguration<AgentRuntimeConsoleConfig>();
            builder.Services.AddCasCapConfiguration<AgentRuntimeAzureAuthConfig>();
            var agentRuntimeClient = builder.Services.AddAgentRuntimeClient();
            var runtimeAuthConfig = builder.Configuration
                .GetSection(AgentRuntimeAzureAuthConfig.ConfigurationSectionName)
                .Get<AgentRuntimeAzureAuthConfig>() ?? new AgentRuntimeAzureAuthConfig();
            if (runtimeAuthConfig.Enabled)
            {
                builder.Services.AddTransient(serviceProvider =>
                {
                    var authOptions = serviceProvider.GetRequiredService<IOptions<AgentRuntimeAzureAuthConfig>>();
                    return new TokenCredentialBearerHandler(
                        authOptions.Value.TokenCredential,
                        authOptions.Value.Scope!);
                });
                agentRuntimeClient.AddHttpMessageHandler<TokenCredentialBearerHandler>();
            }
            builder.Services.AddSingleton<ConsoleApp>();

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
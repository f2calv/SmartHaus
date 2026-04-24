namespace CasCap.Extensions;

/// <summary>
/// Extension methods for registering signal-cli services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the signal-cli client, health check, and configuration.
    /// When <see cref="SignalCliConfig.TransportMode"/> is <see cref="SignalCliTransport.JsonRpc"/> or
    /// <see cref="SignalCliTransport.JsonRpcNative"/>, the <see cref="SignalCliJsonRpcClientService"/>
    /// (WebSocket-based receive) is registered as <see cref="INotifier"/>; otherwise the polling-based
    /// <see cref="SignalCliRestClientService"/> is used.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="isDevelopment">Whether the application is running in development mode.</param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    public static void AddSignalCli(this IServiceCollection services, IConfiguration configuration,
        bool isDevelopment,
        Action<SignalCliConfig>? configure = null)
    {
        var config = services.AddAndGetCasCapConfiguration<SignalCliConfig>(configuration, configure);

        services.AddHttpClient(nameof(SignalCliConnectionHealthCheck), (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<SignalCliConfig>>().Value;
            client.BaseAddress = new Uri(opts.BaseAddress);
            if (isDevelopment)
            {
                var authOpts = sp.GetRequiredService<IOptions<ApiAuthConfig>>().Value;
                client.SetBasicAuth(authOpts.Username, authOpts.Password);
            }
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
        .AddStandardResilience(nameof(SignalCliConnectionHealthCheck));

        services.AddSingleton<SignalCliRestClientService>();

        if (config.TransportMode is SignalCliTransport.JsonRpc or SignalCliTransport.JsonRpcNative)
        {
            services.AddSingleton<INotifier>(sp =>
            {
                Action<System.Net.WebSockets.ClientWebSocket>? configureWebSocket = null;
                if (isDevelopment)
                {
                    var authOpts = sp.GetRequiredService<IOptions<ApiAuthConfig>>().Value;
                    configureWebSocket = ws => ws.SetBasicAuth(authOpts.Username, authOpts.Password);
                }
                return new SignalCliJsonRpcClientService(
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger<SignalCliJsonRpcClientService>(),
                    sp.GetRequiredService<IOptions<SignalCliConfig>>(),
                    sp.GetRequiredService<SignalCliRestClientService>(),
                    configureWebSocket);
            });
        }
        else
            services.AddSingleton<INotifier, SignalCliRestClientService>();

        services.AddSingleton<SignalCliConnectionHealthCheck>();

        if (config.HealthCheck != KubernetesProbeTypes.None)
            services.AddHealthChecks()
                .AddCheck<SignalCliConnectionHealthCheck>(nameof(SignalCliConnectionHealthCheck), tags: config.HealthCheck.GetTags());
    }
}

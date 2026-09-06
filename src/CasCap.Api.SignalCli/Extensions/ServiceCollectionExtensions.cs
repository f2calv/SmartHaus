namespace CasCap.Extensions;

/// <summary>
/// Extension methods for registering signal-cli services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the signal-cli client, health check, and configuration.
    /// <see cref="ISignalCliClient"/> always resolves to <see cref="SignalCliRestClientService"/>, since the
    /// REST surface is identical in every transport mode.
    /// When <see cref="SignalCliConfig.TransportMode"/> is <see cref="SignalCliTransport.JsonRpc"/> or
    /// <see cref="SignalCliTransport.JsonRpcNative"/>, the <see cref="SignalCliJsonRpcClientService"/>
    /// (WebSocket-based receive) is registered as <see cref="INotifier"/> and
    /// <see cref="ISignalCliReceiver"/>; otherwise the polling-based
    /// <see cref="SignalCliRestClientService"/> is used for both.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    public static IServiceCollection AddSignalCli(this IServiceCollection services, IConfiguration configuration,
        Action<SignalCliConfig>? configure = null)
    {
        //First call wins; repeating it would duplicate the named client configuration and every singleton below.
        if (services.Any(descriptor => descriptor.ServiceType == typeof(SignalCliRestClientService)))
            return services;

        var config = services.AddAndGetCasCapConfiguration<SignalCliConfig>(configuration, configure);

        services.AddHttpClient(nameof(SignalCliConnectionHealthCheck), (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<SignalCliConfig>>().Value;
            client.BaseAddress = new Uri(opts.BaseAddress);
            if (opts.BasicAuthEnabled)
            {
                var authOpts = sp.GetRequiredService<IOptions<ApiAuthConfig>>().Value;
                client.SetBasicAuth(authOpts.Username, authOpts.Password);
            }
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
        .AddStandardResilience(nameof(SignalCliConnectionHealthCheck));

        services.AddSingleton<SignalCliRestClientService>();
        services.AddSingleton<ISignalCliClient>(sp => sp.GetRequiredService<SignalCliRestClientService>());

        if (config.TransportMode is SignalCliTransport.JsonRpc or SignalCliTransport.JsonRpcNative)
        {
            services.AddSingleton(sp =>
            {
                Action<System.Net.WebSockets.ClientWebSocket>? configureWebSocket = null;
                if (config.BasicAuthEnabled)
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
            services.AddSingleton<INotifier>(sp => sp.GetRequiredService<SignalCliJsonRpcClientService>());
            services.AddSingleton<ISignalCliReceiver>(sp => sp.GetRequiredService<SignalCliJsonRpcClientService>());
        }
        else
        {
            services.AddSingleton<INotifier>(sp => sp.GetRequiredService<SignalCliRestClientService>());
            services.AddSingleton<ISignalCliReceiver>(sp => sp.GetRequiredService<SignalCliRestClientService>());
        }

        services.AddSingleton<SignalCliConnectionHealthCheck>();

        if (config.HealthCheck != KubernetesProbeTypes.None)
            services.AddHealthChecks()
                .AddCheck<SignalCliConnectionHealthCheck>(nameof(SignalCliConnectionHealthCheck), tags: config.HealthCheck.GetTags());

        return services;
    }
}

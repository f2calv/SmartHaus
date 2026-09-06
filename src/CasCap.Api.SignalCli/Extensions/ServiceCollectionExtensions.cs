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
            if (ResolveBasicAuth(sp, opts) is { } auth)
                client.SetBasicAuth(auth.username, auth.password);
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
        .AddStandardResilience(nameof(SignalCliConnectionHealthCheck));

        services.AddSingleton<SignalCliRestClientService>();
        services.AddSingleton<ISignalCliClient>(sp => sp.GetRequiredService<SignalCliRestClientService>());

        if (config.TransportMode is SignalCliTransport.JsonRpc or SignalCliTransport.JsonRpcNative)
        {
            services.AddSingleton(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<SignalCliConfig>>();
                Action<System.Net.WebSockets.ClientWebSocket>? configureWebSocket = null;
                if (ResolveBasicAuth(sp, opts.Value) is { } auth)
                    configureWebSocket = ws => ws.SetBasicAuth(auth.username, auth.password);
                return new SignalCliJsonRpcClientService(
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger<SignalCliJsonRpcClientService>(),
                    opts,
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

    /// <summary>
    /// Resolves the HTTP Basic credentials to attach to signal-cli requests, or <see langword="null"/>
    /// when <see cref="SignalCliConfig.BasicAuthEnabled"/> is not set.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Basic auth is enabled but no credentials were found in either configuration section.
    /// </exception>
    private static (string username, string password)? ResolveBasicAuth(IServiceProvider sp, SignalCliConfig config)
    {
        if (!config.BasicAuthEnabled)
            return null;

        if (!string.IsNullOrWhiteSpace(config.Username) && !string.IsNullOrWhiteSpace(config.Password))
            return (config.Username, config.Password);

        //Hosts that bind one set of ingress credentials for every API they call get them for free.
        var shared = sp.GetService<IOptions<ApiAuthConfig>>()?.Value;
        if (!string.IsNullOrWhiteSpace(shared?.Username) && !string.IsNullOrWhiteSpace(shared.Password))
            return (shared.Username, shared.Password);

        throw new InvalidOperationException(
            $"{SignalCliConfig.ConfigurationSectionName}:{nameof(SignalCliConfig.BasicAuthEnabled)} is true but no credentials were found. " +
            $"Set {SignalCliConfig.ConfigurationSectionName}:{nameof(SignalCliConfig.Username)} and " +
            $"{SignalCliConfig.ConfigurationSectionName}:{nameof(SignalCliConfig.Password)}, or register " +
            $"{nameof(ApiAuthConfig)} from the {ApiAuthConfig.ConfigurationSectionName} section.");
    }
}

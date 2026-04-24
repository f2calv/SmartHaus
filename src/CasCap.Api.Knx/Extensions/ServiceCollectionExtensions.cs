namespace CasCap.Extensions;

/// <summary>
/// Extension methods for registering KNX bus services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers KNX bus services, event sinks, and health checks.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers only the query service and its minimal
    /// dependencies (options, sinks, state, brokers) without background services,
    /// health checks, or <see cref="IFeature{T}"/> implementations.
    /// Commands are sent inline via the outgoing broker instead of being queued
    /// for <see cref="KnxAutomationBgService"/>.
    /// </param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    /// <param name="additionalSinkAssemblies">Additional assemblies to scan for event sinks.</param>
    public static void AddKnx(this IServiceCollection services, IConfiguration configuration,
        bool lite = false,
        Action<KnxConfig>? configure = null,
        params Assembly[] additionalSinkAssemblies)
    {
        var config = services.AddAndGetCasCapConfiguration<KnxConfig>(configuration, c =>
        {
            configure?.Invoke(c);
            if (lite)
                c.LiteMode = true;
        });

        // Auto-register all sinks decorated with [SinkType] whose type is enabled
        services.AddEventSinks<KnxEvent>(config.Sinks, typeof(ServiceCollectionExtensions).Assembly);

        // In lite mode skip background services but still use Redis state if configured,
        // so that MCP/Comms agents read live state written by the real Knx service process.
        if (config.Sinks.AvailableSinks.TryGetValue("Redis", out var redisConfig) && redisConfig.Enabled)
            services.AddSingleton<IKnxState, KnxRedisStateService>();
        else
            services.AddSingleton<IKnxState, KnxMemoryStateService>();

        services.AddSingleton<IStateChangeQueue, StateChangeQueue>();

        // Register telegram brokers based on configured mode
        if (config.TelegramBrokerMode is TelegramBrokerMode.Channel)
        {
            services.AddSingleton<IKnxTelegramBroker<KnxEvent>, ChannelKnxTelegramBroker<KnxEvent>>();
            services.AddSingleton<IKnxTelegramBroker<KnxOutgoingTelegram>, ChannelKnxTelegramBroker<KnxOutgoingTelegram>>();
        }
        else
        {
            services.AddSingleton<IKnxTelegramBroker<KnxEvent>>(sp =>
            {
                var knxOpts = sp.GetRequiredService<IOptions<KnxConfig>>().Value;
                return new RedisKnxTelegramBroker<KnxEvent>(
                    sp.GetRequiredService<ILogger<RedisKnxTelegramBroker<KnxEvent>>>(),
                    sp.GetRequiredService<IRemoteCache>(),
                    knxOpts.TelegramStreamKeyIncoming,
                    knxOpts.TelegramConsumerGroup,
                    knxOpts.TelegramConsumerGroupStartId,
                    knxOpts.TelegramStreamReadPosition,
                    knxOpts.TelegramStreamReadCount,
                    knxOpts.TelegramStreamPollingDelayMs,
                    knxOpts.TelegramStreamExpiryDays);
            });
            services.AddSingleton<IKnxTelegramBroker<KnxOutgoingTelegram>>(sp =>
            {
                var knxOpts = sp.GetRequiredService<IOptions<KnxConfig>>().Value;
                return new RedisKnxTelegramBroker<KnxOutgoingTelegram>(
                    sp.GetRequiredService<ILogger<RedisKnxTelegramBroker<KnxOutgoingTelegram>>>(),
                    sp.GetRequiredService<IRemoteCache>(),
                    knxOpts.TelegramStreamKeyOutgoing,
                    knxOpts.TelegramConsumerGroup,
                    knxOpts.TelegramConsumerGroupStartId,
                    knxOpts.TelegramStreamReadPosition,
                    knxOpts.TelegramStreamReadCount,
                    knxOpts.TelegramStreamPollingDelayMs,
                    knxOpts.TelegramStreamExpiryDays);
            });
        }

        // Registered outside the lite guard because KnxGroupAddressLookupService depends on it
        services.AddSingleton<KnxGroupAddressLookupHealthCheck>();
        services.AddSingleton<KnxGroupAddressLookupService>();

        if (!lite)
        {
            services.AddSingleton<KnxConnectionHealthCheck>();

            if (config.HealthCheck != KubernetesProbeTypes.None)
            {
                services.AddHealthChecks()
                    .AddCheck<KnxGroupAddressLookupHealthCheck>(nameof(KnxGroupAddressLookupHealthCheck), tags: config.HealthCheck.GetTags());

                services.AddHealthChecks()
                    .AddCheck<KnxConnectionHealthCheck>(nameof(KnxConnectionHealthCheck), tags: config.HealthCheck.GetTags());
            }

            services.AddSingleton<KnxConnectionNotifier>();
            services.AddSingleton<IKnxConnectionNotifier>(sp => sp.GetRequiredService<KnxConnectionNotifier>());
            services.AddSingleton<IBgFeature, KnxMonitorBgService>();
            services.AddSingleton<IBgFeature, KnxSenderBgService>();
            services.AddSingleton<IBgFeature, KnxProcessorBgService>();
            services.AddSingleton<IBgFeature, KnxAutomationBgService>();
        }

        services.AddSingleton<KnxQueryService>();
        services.AddSingleton<IKnxQueryService>(sp => sp.GetRequiredService<KnxQueryService>());
    }
}

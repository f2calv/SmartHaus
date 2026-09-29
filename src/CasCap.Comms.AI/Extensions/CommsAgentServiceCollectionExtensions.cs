namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the AI agent responder for the shared Signalizr communications pipeline.</summary>
public static class CommsAgentServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AgentCommsResponder"/> as the <see cref="ICommsResponder"/>, with its
    /// <see cref="CommsDebugNotifier"/>, session store, command handler and poll tracker.
    /// </summary>
    /// <remarks>
    /// The keyed <see cref="Microsoft.Agents.AI.AIAgent"/> named <paramref name="agentKey"/> and its
    /// <see cref="AIConfig"/> profile are registered by the host. Register <see cref="IAgentRunEnricher"/>
    /// implementations to add host-specific measurements.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="agentKey">The key of the agent in <see cref="AIConfig.Agents"/>.</param>
    /// <param name="instructionsAssembly">The assembly holding the agent's embedded instruction resources.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCommsAgent(this IServiceCollection services, string agentKey,
        Assembly instructionsAssembly)
    {
        services.AddSingleton(new CommsAgentProfile(agentKey, instructionsAssembly));
        services.TryAddSingleton<DistributedCacheSessionStore>();
        services.TryAddSingleton<ISessionStore>(sp => sp.GetRequiredService<DistributedCacheSessionStore>());
        services.TryAddSingleton<AgentCommandHandler>();
        services.TryAddSingleton<CommsDebugNotifier>();
        services.TryAddSingleton<ICommsResponder, AgentCommsResponder>();
        services.AddMessagingMcp();
        return services;
    }

    /// <summary>
    /// Registers the <see cref="MessagingMcpQueryService"/> that exposes poll operations on the
    /// <see cref="CommsConfig.GroupName"/> group as MCP tools.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddMessagingMcp(this IServiceCollection services)
    {
        services.TryAddSingleton<IPollTracker, InMemoryPollTracker>();
        services.TryAddSingleton(sp => new MessagingMcpQueryService(
            sp.GetRequiredService<ISignalizrClient>(),
            sp.GetRequiredService<IPollTracker>(),
            sp.GetRequiredService<IOptions<CommsConfig>>().Value.GroupName));
        return services;
    }

    /// <summary>
    /// Registers a stub <see cref="MessagingMcpQueryService"/> so agent configurations that reference it
    /// pass dependency-injection validation without a Signalizr gateway.
    /// </summary>
    /// <remarks>Its tools fail at invocation time. Use it only where the <c>Comms</c> feature is disabled.</remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddMessagingMcpStub(this IServiceCollection services)
    {
        services.TryAddSingleton<IPollTracker, InMemoryPollTracker>();
        services.TryAddSingleton(sp => new MessagingMcpQueryService(
            null!,
            sp.GetRequiredService<IPollTracker>(),
            string.Empty));
        return services;
    }
}

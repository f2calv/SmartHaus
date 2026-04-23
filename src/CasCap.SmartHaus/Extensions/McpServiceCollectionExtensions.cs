namespace CasCap.Extensions;

/// <summary>
/// Extension methods for registering MCP tools from the <c>CasCap.SmartHaus.Mcp</c> assembly.
/// </summary>
public static class HausMcpServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="Services.SystemMcpQueryService"/> to expose general system tools available to all agents.
    /// </summary>
    public static void AddSystemMcp(this IServiceCollection services) =>
        services.AddSingleton<SystemMcpQueryService>();

    /// <summary>
    /// Registers the <see cref="Services.HeatPumpMcpQueryService"/> to expose heat pump data via MCP tools.
    /// </summary>
    public static void AddHeatPumpMcp(this IServiceCollection services) =>
        services.AddSingleton<HeatPumpMcpQueryService>();

    /// <summary>
    /// Registers the <see cref="Services.FrontDoorMcpQueryService"/> to expose front door intercom data via MCP tools.
    /// </summary>
    public static void AddFrontDoorMcp(this IServiceCollection services) =>
        services.AddSingleton<FrontDoorMcpQueryService>();

    /// <summary>
    /// Registers the <see cref="Services.InverterMcpQueryService"/> to expose solar inverter data via MCP tools.
    /// </summary>
    public static void AddInverterMcp(this IServiceCollection services) =>
        services.AddSingleton<InverterMcpQueryService>();

    /// <summary>
    /// Registers the <see cref="Services.BusSystemMcpQueryService"/> to expose bus system data via MCP tools.
    /// </summary>
    public static void AddBusSystemMcp(this IServiceCollection services) =>
        services.AddSingleton<BusSystemMcpQueryService>();

    /// <summary>
    /// Registers the <see cref="Services.AppliancesMcpQueryService"/> to expose home appliance data via MCP tools.
    /// </summary>
    public static void AddAppliancesMcp(this IServiceCollection services) =>
        services.AddSingleton<AppliancesMcpQueryService>();

    /// <summary>
    /// Registers the <see cref="Services.EdgeHardwareMcpQueryService"/> to expose edge hardware monitoring data via MCP tools.
    /// </summary>
    public static void AddEdgeHardwareMcp(this IServiceCollection services) =>
        services.AddSingleton<EdgeHardwareMcpQueryService>();

    /// <summary>
    /// Registers the <see cref="Services.IpCameraMcpQueryService"/> to expose IP camera data via MCP tools.
    /// </summary>
    public static void AddCamerasMcp(this IServiceCollection services) =>
        services.AddSingleton<IpCameraMcpQueryService>();

    /// <summary>
    /// Registers the <see cref="Services.AquariumMcpQueryService"/> to expose aquarium pump data via MCP tools.
    /// </summary>
    public static void AddAquariumMcp(this IServiceCollection services) =>
        services.AddSingleton<AquariumMcpQueryService>();

    /// <summary>
    /// Registers the <see cref="Services.SmartPlugMcpQueryService"/> to expose smart plug operations via MCP tools.
    /// </summary>
    public static void AddSmartPlugMcp(this IServiceCollection services) =>
        services.AddSingleton<SmartPlugMcpQueryService>();

    /// <summary>
    /// Registers the <see cref="Services.SmartLightingMcpQueryService"/> to expose smart lighting data via MCP tools.
    /// </summary>
    public static void AddSmartLightingMcp(this IServiceCollection services) =>
        services.AddSingleton(sp => new SmartLightingMcpQueryService(
            sp.GetService<IKnxQueryService>(),
            sp.GetService<IWizQueryService>(),
            sp.GetService<IShellyQueryService>()));

    /// <summary>
    /// Registers the <see cref="Services.MessagingMcpQueryService"/> to expose messaging poll operations as MCP tools.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="phoneNumber">The Signal account phone number.</param>
    /// <param name="groupName">The Signal group name to target with poll operations.</param>
    public static void AddMessagingMcp(this IServiceCollection services, string phoneNumber, string groupName)
    {
        services.AddSingleton<IPollTracker, InMemoryPollTracker>();
        services.AddSingleton(sp => new MessagingMcpQueryService(
            sp.GetRequiredService<SignalCliRestClientService>(),
            sp.GetRequiredService<IPollTracker>(),
            phoneNumber,
            groupName));
    }

    /// <summary>
    /// Registers a stub <see cref="Services.MessagingMcpQueryService"/> so agent configs that reference
    /// it pass DI validation without requiring Signal CLI or Comms infrastructure.
    /// </summary>
    /// <remarks>Tools will fail at invocation time if actually called — use only when Comms is disabled.</remarks>
    public static void AddMessagingMcpStub(this IServiceCollection services)
    {
        services.AddSingleton<IPollTracker, InMemoryPollTracker>();
        services.AddSingleton(sp => new MessagingMcpQueryService(
            null!,
            sp.GetRequiredService<IPollTracker>(),
            string.Empty,
            string.Empty));
    }
}

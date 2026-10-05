namespace CasCap.Extensions;

/// <summary>
/// Extension methods for registering MCP tools from the <c>CasCap.SmartHaus</c> assembly.
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
}

namespace CasCap.Services;

/// <summary>
/// MCP tools providing general system information available to all agents.
/// </summary>
[McpServerToolType]
public partial class SystemMcpQueryService(ILogger<SystemMcpQueryService> logger, IOptions<AIConfig> aiConfig)
{
    /// <summary>
    /// Returns the current date, time and UTC offset for the configured house time zone.
    /// </summary>
    [McpServerTool]
    [Description("Current local date/time, day-of-week and UTC offset for the house time zone.")]
    public DateTimeState GetCurrentDatetimeState()
    {
        logger.LogDebug("{ClassName} {MethodName} invoked", nameof(SystemMcpQueryService), nameof(GetCurrentDatetimeState));

        var timeZoneId = aiConfig.Value.TimeZoneId;
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException ex)
        {
            logger.LogError(ex, "{ClassName} time zone {TimeZoneId} not found on this system, falling back to UTC", nameof(SystemMcpQueryService), timeZoneId);
            tz = TimeZoneInfo.Utc;
        }

        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var result = new DateTimeState
        {
            LocalTime = now,
            DayOfWeek = now.DayOfWeek.ToString(),
            UtcOffset = tz.GetUtcOffset(now).ToString(),
            TimeZone = tz.Id,
        };

        logger.LogDebug("{ClassName} {MethodName} returning {LocalTime} {DayOfWeek} {UtcOffset} {TimeZone}",
            nameof(SystemMcpQueryService), nameof(GetCurrentDatetimeState), result.LocalTime, result.DayOfWeek, result.UtcOffset, result.TimeZone);

        return result;
    }

    /// <summary>
    /// Returns all configured AI providers (connection type, model, endpoint).
    /// </summary>
    [McpServerTool]
    [Description("Lists all configured AI providers with their type, model name and endpoint. Use to discover which models are available.")]
    public ProviderInfo[] GetProviders()
    {
        logger.LogDebug("{ClassName} {MethodName} invoked", nameof(SystemMcpQueryService), nameof(GetProviders));

        var providers = aiConfig.Value.Providers
            .Select(kvp => new ProviderInfo
            {
                Key = kvp.Key,
                Type = kvp.Value.Type.ToString(),
                ModelName = kvp.Value.ModelName,
                Endpoint = kvp.Value.Endpoint?.ToString(),
                ReasoningEffort = kvp.Value.ReasoningEffort?.ToString(),
            })
            .ToArray();

        logger.LogDebug("{ClassName} {MethodName} returning {Count} provider(s)",
            nameof(SystemMcpQueryService), nameof(GetProviders), providers.Length);

        return providers;
    }

    /// <summary>
    /// Returns all configured AI agents with their role, provider, delegation targets and status.
    /// </summary>
    [McpServerTool]
    [Description("Lists all configured AI agents with their name, description, enabled status, backing provider and sub-agent delegations. Use to discover system capabilities.")]
    public AgentInfo[] GetAgents()
    {
        logger.LogDebug("{ClassName} {MethodName} invoked", nameof(SystemMcpQueryService), nameof(GetAgents));

        var agents = aiConfig.Value.Agents
            .Select(kvp => new AgentInfo
            {
                Key = kvp.Key,
                Name = kvp.Value.Name,
                Description = kvp.Value.Description,
                Enabled = kvp.Value.Enabled,
                Provider = kvp.Value.Provider,
                MaxMessages = kvp.Value.MaxMessages,
                ToolSourceCount = kvp.Value.Tools.Length,
                DelegatedAgents = kvp.Value.Tools
                    .Where(t => !string.IsNullOrWhiteSpace(t.Agent))
                    .Select(t => t.Agent!)
                    .ToArray(),
            })
            .ToArray();

        logger.LogDebug("{ClassName} {MethodName} returning {Count} agent(s)",
            nameof(SystemMcpQueryService), nameof(GetAgents), agents.Length);

        return agents;
    }
}

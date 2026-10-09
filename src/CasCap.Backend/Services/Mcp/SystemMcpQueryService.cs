namespace CasCap.Services;

/// <summary>
/// MCP tools providing general system information available to all agents.
/// </summary>
public sealed partial class SystemMcpQueryService(ILogger<SystemMcpQueryService> logger, IOptions<AppConfig> appConfig, TimeProvider timeProvider)
{
    /// <summary>
    /// Returns the current date, time and UTC offset for the configured house time zone.
    /// </summary>
    [McpServerTool]
    [Description("Current local date/time, day-of-week and UTC offset for the house time zone.")]
    public DateTimeState GetCurrentDatetimeState()
    {
        logger.LogDebug("{ClassName} {MethodName} invoked", nameof(SystemMcpQueryService), nameof(GetCurrentDatetimeState));

        var timeZoneId = appConfig.Value.TimeZoneId;
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

        var now = TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, tz);
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
}

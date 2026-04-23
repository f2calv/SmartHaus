using Microsoft.Extensions.AI;

namespace CasCap.Models;

/// <summary>
/// MCP server prompts for heat pump interactions.
/// </summary>
[McpServerPromptType]
public static partial class HeatPumpPrompts
{
    /// <summary>
    /// Creates a prompt to retrieve a full snapshot of all heat pump values and summarise the heating system status.
    /// </summary>
    [McpServerPrompt]
    public static partial ChatMessage SummariseHeatingStatus() =>
        new(ChatRole.User,
            """
            Retrieve the current heat pump state using the GetHeatPumpState tool.
            Summarise the heating system status including:
            - Outdoor temperature
            - Supply and return temperatures for each heating circuit
            - Domestic hot water (DHW) temperature and setpoint
            - Current operating mode and any active programs
            - Any error codes or warnings
            Present the information in a clear, structured format.
            """);

    /// <summary>
    /// Creates a prompt to analyse the heating efficiency by reviewing recent sensor data trends.
    /// </summary>
    /// <param name="sensorId">The heat pump sensor identifier to analyse (e.g. _heatingCircuits_hc2_supplyTemperatureSetpoint).</param>
    [McpServerPrompt]
    public static partial ChatMessage AnalyseSensorTrend(
        string sensorId = "_heatingCircuits_hc2_supplyTemperatureSetpoint") =>
        new(ChatRole.User,
            $"""
            Retrieve historical events for the heat pump sensor "{sensorId}" using the
            GetEventsById tool. Analyse the trend over time and report:
            - The current value and recent min/max range
            - Any significant changes or patterns
            - Whether the values look normal for the current season
            Provide a brief recommendation if anything looks unusual.
            """);

    /// <summary>
    /// Creates a prompt to check domestic hot water readiness and temperature.
    /// </summary>
    [McpServerPrompt]
    public static partial ChatMessage CheckHotWater() =>
        new(ChatRole.User,
            """
            Retrieve the current heat pump state using the GetHeatPumpState tool.
            Focus on the domestic hot water (DHW) circuit and report:
            - Current DHW temperature vs setpoint
            - Whether the DHW is currently being heated
            - The active DHW time program
            - Whether the temperature is sufficient for comfortable use
            Flag if the water temperature is below the setpoint or if there are any issues.
            """);

    /// <summary>
    /// Creates a prompt to perform a health check of the Buderus heating system.
    /// </summary>
    [McpServerPrompt]
    public static partial ChatMessage CheckSystemHealth() =>
        new(ChatRole.User,
            """
            Retrieve the current heat pump state using the GetHeatPumpState tool.
            Perform a health check of the heating system:
            - Check for any active error codes or fault conditions
            - Verify that supply temperatures are within expected ranges
            - Check that the heat pump is operating in the correct mode
            - Inspect pressure values if available
            - Report the system uptime and any recent restarts
            Provide a clear pass/fail summary with details on any issues found.
            """);

    /// <summary>
    /// Creates a prompt to compare heating circuit temperatures and identify imbalances.
    /// </summary>
    [McpServerPrompt]
    public static partial ChatMessage CompareHeatingCircuits() =>
        new(ChatRole.User,
            """
            Retrieve the current heat pump state using the GetHeatPumpState tool.
            Compare the heating circuits (hc1, hc2, etc.) and report:
            - Supply temperature and setpoint for each circuit
            - Room temperature vs setpoint for each circuit (if available)
            - Current operating mode for each circuit
            - Any significant differences between circuits that might indicate an imbalance
            Suggest adjustments if any circuit appears to be underperforming.
            """);
}

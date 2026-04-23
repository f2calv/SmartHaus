using Microsoft.Extensions.AI;

namespace CasCap.Models;

/// <summary>
/// MCP server prompts for solar inverter interactions.
/// </summary>
[McpServerPromptType]
public static partial class InverterPrompts
{
    /// <summary>
    /// Creates a prompt to retrieve a real-time solar production summary and describe the current energy balance.
    /// </summary>
    [McpServerPrompt]
    public static partial ChatMessage SummariseSolarStatus() =>
        new(ChatRole.User,
            """
            Retrieve the current solar snapshot using the GetInverterSnapshot tool.
            Report the current state of the solar system including:
            - PV power generation (P_PV)
            - Grid power (P_Grid) — positive means consuming, negative means feeding
            - Battery power (P_Akku) — positive means discharging, negative means charging
            - Household consumption (P_Load)
            - Battery state of charge (SOC)
            Explain whether the home is currently self-sufficient, drawing from grid, or
            feeding surplus energy back to the grid.
            """);

    /// <summary>
    /// Creates a prompt to analyse today's solar production trend from historical readings.
    /// </summary>
    /// <param name="limit">Maximum number of readings to retrieve.</param>
    [McpServerPrompt]
    public static partial ChatMessage AnalyseDailyProduction(
        int limit = 200) =>
        new(ChatRole.User,
            $"""
            Retrieve the current power flow using the GetInverterPowerFlow tool and the
            snapshot using the GetInverterSnapshot tool. Analyse the current production and report:
            - Current PV generation wattage
            - Grid import vs export balance
            - Battery charge/discharge state
            - Current self-consumption ratio
            Based on the current readings, provide a brief assessment of today's energy performance.
            """);

    /// <summary>
    /// Creates a prompt to check the health of the inverter and connected devices.
    /// </summary>
    [McpServerPrompt]
    public static partial ChatMessage CheckInverterHealth() =>
        new(ChatRole.User,
            """
            Check the health of the solar inverter system:
            1. Use GetInverterInfo to get inverter details and state.
            2. Use GetInverterConnectedDevices to list all connected devices (inverters, meters, storage).
            3. Use GetInverterElectricalReadings to check AC/DC voltages and currents.
            Report the inverter state, any error codes, and whether all expected devices are
            connected. Flag any readings that appear abnormal.
            """);

    /// <summary>
    /// Creates a prompt to provide a detailed power flow breakdown across all sources and sinks.
    /// </summary>
    [McpServerPrompt]
    public static partial ChatMessage DetailedPowerFlow() =>
        new(ChatRole.User,
            """
            Retrieve the full power flow data using the GetInverterPowerFlow tool and
            meter data using the GetInverterMeterReadings tool.
            Provide a detailed breakdown of power flow:
            - Per-phase voltage, current and power from the meter
            - Per-inverter production and battery mode
            - Site-level autonomy and self-consumption percentages
            - Grid import/export balance
            Present the data in a structured table format.
            """);

    /// <summary>
    /// Creates a prompt to check the battery storage status and estimate remaining capacity.
    /// </summary>
    [McpServerPrompt]
    public static partial ChatMessage CheckBatteryStatus() =>
        new(ChatRole.User,
            """
            Retrieve the battery storage data using the GetInverterBatteryStatus tool and the
            current solar snapshot using the GetInverterSnapshot tool.
            Report:
            - Current state of charge (SOC) percentage
            - Whether the battery is charging, discharging, or idle
            - Battery cell temperature
            - Designed vs maximum capacity
            - Estimated time until full charge or depletion at current rate
            Flag any issues with battery health indicators.
            """);
}

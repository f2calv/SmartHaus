using Microsoft.Extensions.AI;

namespace CasCap.Models;

/// <summary>
/// MCP server prompts for home appliance interactions.
/// </summary>
[McpServerPromptType]
public static partial class AppliancesPrompts
{
    /// <summary>
    /// Creates a prompt to retrieve all household appliances and summarise their current status.
    /// </summary>
    [McpServerPrompt]
    public static partial ChatMessage SummariseAllAppliances() =>
        new(ChatRole.User,
            """
            Retrieve all household appliances using the GetAllAppliances tool.
            Summarise the status of each appliance including:
            - Device name and type (e.g. dishwasher, washing machine, oven)
            - Current status (off, on, running, finished, etc.)
            - If running: the current program, program phase, and remaining time
            - Any active signals (info, failure, door open)
            Present a clear overview of which appliances are active and which are idle.
            """);

    /// <summary>
    /// Creates a prompt to check the status of a specific appliance by its device ID.
    /// </summary>
    /// <param name="deviceId">The device ID (serial number) of the appliance to check.</param>
    [McpServerPrompt]
    public static partial ChatMessage CheckApplianceStatus(
        string deviceId) =>
        new(ChatRole.User,
            $"""
            Retrieve the full state of the appliance with device ID "{deviceId}" using
            the GetApplianceState tool, and its identity using the GetApplianceIdentification tool.
            Report:
            - Device name, type, and serial number
            - Current status and program phase
            - Remaining time and elapsed time (if running)
            - Temperature readings (target and current)
            - Eco feedback (water and energy consumption) if available
            - Whether remote control is enabled
            - Any active signals or faults
            """);

    /// <summary>
    /// Creates a prompt to list the available programs for an appliance and recommend one.
    /// </summary>
    /// <param name="deviceId">The device ID (serial number) of the appliance.</param>
    /// <param name="loadType">A description of what needs to be washed or cooked (e.g. "heavily soiled pots", "delicate shirts").</param>
    [McpServerPrompt]
    public static partial ChatMessage RecommendProgram(
        string deviceId,
        string loadType = "normal everyday load") =>
        new(ChatRole.User,
            $"""
            Retrieve the available programs for the appliance with device ID "{deviceId}"
            using the GetAppliancePrograms tool, and the available actions using the
            GetApplianceActions tool.
            Based on the load type "{loadType}", recommend the most suitable program.
            Include the program name, expected temperature range and duration if available.
            Explain why this program is the best choice but do NOT start the program.
            """);

    /// <summary>
    /// Creates a prompt to check which appliances have finished their cycle and need attention.
    /// </summary>
    [McpServerPrompt]
    public static partial ChatMessage CheckFinishedAppliances() =>
        new(ChatRole.User,
            """
            Retrieve all household appliances using the GetAllAppliances tool.
            Identify any appliances that have finished their cycle (status indicates finished
            or end of program) and need to be unloaded or attended to.
            For each finished appliance, report the device name, type, and how long ago it
            finished (if elapsed time is available). Also check the signalDoor flag to see
            if the door has been opened since the cycle ended.
            Compose a brief reminder message for the household.
            """);

    /// <summary>
    /// Creates a prompt to review eco feedback data for all running or recently finished appliances.
    /// </summary>
    [McpServerPrompt]
    public static partial ChatMessage ReviewEcoFeedback() =>
        new(ChatRole.User,
            """
            Retrieve all household appliances using the GetAllAppliances tool.
            For each appliance that is running or recently finished, check the eco feedback
            data (water and energy consumption) from the state.
            Report:
            - Water consumption (current and forecast) per appliance
            - Energy consumption (current and forecast) per appliance
            - Which appliances are the most/least efficient in the current cycle
            Provide tips on how to reduce consumption if forecasts indicate high usage.
            """);
}

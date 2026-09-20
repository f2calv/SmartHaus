using Microsoft.Extensions.AI;

namespace CasCap.Models;

/// <summary>
/// MCP server prompts for bus system smart-home interactions.
/// </summary>
[McpServerPromptType]
public static partial class BusSystemPrompts
{
    /// <summary>
    /// Creates a prompt to retrieve all KNX group address state and summarise the current status of the home.
    /// </summary>
    [McpServerPrompt]
    public static partial ChatMessage SummariseHomeStatus() =>
        new(ChatRole.User,
            """
            Retrieve all active group addresses using the GetHouseGroupAddresses tool.
            Summarise the current status of the home including:
            - Which lights are on and their dimming levels (category LI)
            - Shutter/blind positions (category BL)
            - Heating setpoints and current temperatures (category HZ)
            - Any open doors or windows detected by binary contacts (category BI)
            - Presence/motion detection status (category PM)
            Present a clear floor-by-floor overview (KG, EG, OG, DG).
            """);

    /// <summary>
    /// Creates a prompt to list and describe the KNX group addresses for a specific floor.
    /// </summary>
    /// <param name="floor">The floor abbreviation to inspect (e.g. EG for ground floor, OG for upper floor).</param>
    [McpServerPrompt]
    public static partial ChatMessage InspectFloor(
        string floor = "EG") =>
        new(ChatRole.User,
            $"""
            Use the GetHouseGroupAddresses tool with groupAddressFilter Active to retrieve
            all active group addresses, then filter the results to floor "{floor}".
            For each room on that floor, list:
            - Lighting state (on/off, dimming percentage)
            - Shutter/blind position
            - Current temperature and heating setpoint
            - Any binary contact states (doors, windows)
            Group the results by room name.
            """);

    /// <summary>
    /// Creates a prompt to check which lights are currently switched on and suggest energy savings.
    /// </summary>
    [McpServerPrompt]
    public static partial ChatMessage CheckLightsAndEnergy() =>
        new(ChatRole.User,
            """
            Use the GetHouseLightSwitchStates tool to retrieve the on/off state of all lights.
            List every light that is currently on, including its floor, room, dimming level
            and how long it has been on (if timestamp data is available).
            Suggest which lights could be turned off to save energy.
            """);

    /// <summary>
    /// Creates a prompt to review heating across all floors and recommend adjustments.
    /// </summary>
    [McpServerPrompt]
    public static partial ChatMessage ReviewHeating() =>
        new(ChatRole.User,
            """
            Use the GetHouseHeatingZones tool to retrieve all heating zones.
            For each room report:
            - Current temperature
            - Heating setpoint
            - Valve output percentage (if available)
            Highlight any rooms where the temperature is significantly above or below the
            setpoint and recommend adjustments.
            """);

    /// <summary>
    /// Creates a prompt to send a command to a named KNX group address.
    /// </summary>
    /// <param name="groupAddressName">The KNX group address name (e.g. EG-LI-Entrance-DL-SW).</param>
    /// <param name="value">The value to send (e.g. True, False, 50).</param>
    [McpServerPrompt]
    public static partial ChatMessage SendCommand(
        string groupAddressName,
        string value) =>
        new(ChatRole.User,
            $"""
            Send the value "{value}" to the KNX group address "{groupAddressName}" using the
            Send2Bus tool. After sending, confirm whether the command was accepted and report
            the result.
            """);
}

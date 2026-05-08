using Microsoft.Extensions.AI;

namespace CasCap.Models;

/// <summary>
/// MCP server prompts for front door intercom interactions.
/// </summary>
[McpServerPromptType]
public static partial class FrontDoorPrompts
{
    /// <summary>
    /// Creates a prompt to analyse a front door camera photo and describe who or what is visible.
    /// </summary>
    /// <param name="context">Optional additional context about why the photo is being analysed (e.g. doorbell rang, motion detected).</param>
    [McpServerPrompt]
    public static partial ChatMessage AnalyseFrontDoorPhoto(
        string context = "Someone is at the front door.") =>
        new(ChatRole.User,
            $"""
            Take a real-time photo from the front door camera using the GetHouseDoorPhoto tool,
            then describe who or what is visible at the front door.
            Context: {context}
            Include details about the number of people, any packages, vehicles or animals visible.
            """);

    /// <summary>
    /// Creates a prompt to review a sequence of historical front door snapshots and summarise the activity.
    /// </summary>
    /// <param name="count">The number of most recent historical images to review.</param>
    [McpServerPrompt]
    public static partial ChatMessage ReviewDoorHistory(
        int count = 5) =>
        new(ChatRole.User,
            $"""
            Retrieve the {count} most recent historical snapshots from the front door camera
            using the GetHistoryImage tool (indices 1 to {count}), then summarise the activity
            over that period.
            Note any recurring visitors, delivery patterns or unusual activity.
            """);

    /// <summary>
    /// Creates a prompt to determine whether it is safe to remotely open the front door based on a live photo.
    /// </summary>
    [McpServerPrompt]
    public static partial ChatMessage ShouldOpenDoor() =>
        new(ChatRole.User,
            """
            Take a real-time photo from the front door camera using the GetHouseDoorPhoto tool.
            Analyse the image and determine whether it appears safe to remotely unlock the
            front door using the UnlockHouseDoor tool.
            Consider: Is the person recognisable? Are there any signs of risk?
            Respond with a clear recommendation and reasoning, but do NOT trigger the unlock.
            """);

    /// <summary>
    /// Creates a prompt to check the current front door device health by inspecting device info and SIP status.
    /// </summary>
    [McpServerPrompt]
    public static partial ChatMessage CheckDeviceHealth() =>
        new(ChatRole.User,
            """
            Check the front door intercom health by retrieving device information and SIP status.
            Report firmware version, uptime if available, connected relays, and whether SIP
            registration is active. Flag any issues that need attention.
            """);

    /// <summary>
    /// Creates a prompt to summarise a front door notification event for a mobile message.
    /// </summary>
    /// <param name="eventType">The type of event that triggered the notification (e.g. doorbell, motionsensor, rfid).</param>
    [McpServerPrompt]
    public static partial ChatMessage SummariseAlert(
        string eventType = "doorbell") =>
        new(ChatRole.User,
            $"""
            A front door {eventType} event has just occurred. Take a real-time photo using the
            GetHouseDoorPhoto tool, analyse the image, and compose a brief mobile notification
            message (max 2 sentences) describing what triggered the alert and what is visible.
            """);
}

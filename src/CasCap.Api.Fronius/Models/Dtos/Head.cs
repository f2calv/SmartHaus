namespace CasCap.Models.Dtos;

/// <summary>
/// Response head containing status information and a timestamp.
/// </summary>
public record Head
{
    /// <summary>
    /// Optional request arguments echoed back by the API.
    /// </summary>
    [Description("Optional request arguments echoed back by the API.")]
    public object? RequestArguments { get; init; }

    /// <summary>
    /// The API response status containing a code, reason and user message.
    /// </summary>
    [Description("The API response status containing a code, reason and user message.")]
    public Status? Status { get; init; }

    /// <summary>
    /// The UTC timestamp of the API response.
    /// </summary>
    [Description("The UTC timestamp of the API response.")]
    public DateTime Timestamp { get; init; }
}

namespace CasCap.Models.Dtos;

/// <summary>
/// API response status block returned in the <see cref="Head"/>.
/// </summary>
public record Status
{
    /// <summary>
    /// The numeric status code (0 = success).
    /// </summary>
    [Description("The numeric status code (0 = success).")]
    public int Code { get; init; }

    /// <summary>
    /// A short reason string describing the status.
    /// </summary>
    [Description("A short reason string describing the status.")]
    public string? Reason { get; init; }

    /// <summary>
    /// A user-facing message describing the status.
    /// </summary>
    [Description("A user-facing message describing the status.")]
    public string? UserMessage { get; init; }
}

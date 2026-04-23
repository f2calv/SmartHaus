namespace CasCap.Models.Dtos;

/// <summary>
/// Standard Fronius API response wrapper containing a <see cref="Head"/> and a typed <see cref="Body{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the <c>Data</c> property inside the response body.</typeparam>
public record ApiWrapper<T>
{
    /// <summary>
    /// The response body containing the typed data payload.
    /// </summary>
    [Description("The response body containing the typed data payload.")]
    public Body<T>? Body { get; init; }

    /// <summary>
    /// The response head containing status, timestamp and optional request arguments.
    /// </summary>
    [Description("The response head containing status, timestamp and optional request arguments.")]
    public Head? Head { get; init; }
}

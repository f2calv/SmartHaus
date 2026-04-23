namespace CasCap.Models.Dtos;

/// <summary>
/// Response body containing a typed <see cref="Data"/> payload.
/// </summary>
/// <typeparam name="T">The type of the data payload.</typeparam>
public record Body<T>
{
    /// <summary>
    /// The data payload returned by the API.
    /// </summary>
    [Description("The data payload returned by the API.")]
    public T? Data { get; init; }
}

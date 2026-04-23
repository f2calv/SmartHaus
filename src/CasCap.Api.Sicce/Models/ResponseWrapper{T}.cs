namespace CasCap.Models;

/// <summary>Generic wrapper for API responses containing data of type <typeparamref name="T"/>.</summary>
/// <typeparam name="T">The type of data contained in the response.</typeparam>
public class ResponseWrapper<T> : Response
{
    /// <summary>
    /// optional object
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }
}

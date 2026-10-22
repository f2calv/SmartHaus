using CasCap.Common.Exceptions;
using System.Net;

namespace CasCap.Exceptions;

/// <summary>Thrown when the upstream Miele API cannot provide a valid response.</summary>
public sealed class MieleApiException(string message, HttpStatusCode? statusCode = null) : GenericException(message)
{
    /// <summary>Gets the upstream HTTP status when a response was received.</summary>
    public HttpStatusCode? StatusCode { get; } = statusCode;
}

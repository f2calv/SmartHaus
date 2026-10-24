using CasCap.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace CasCap.Services;

/// <summary>Maps Miele upstream failures to sanitized gateway Problem Details.</summary>
public sealed class MieleApiExceptionHandler : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not MieleApiException)
            return false;

        await TypedResults.Problem(new ProblemDetails
        {
            Status = StatusCodes.Status502BadGateway,
            Title = "Miele API unavailable",
            Detail = "The appliance provider could not complete the request.",
        }).ExecuteAsync(httpContext);
        return true;
    }
}

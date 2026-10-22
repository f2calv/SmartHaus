using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace CasCap.Tests.Infrastructure;

/// <summary>Exercises the real host JSON and validation pipeline without device dependencies.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/contract-probe")]
public sealed class ApiContractProbeController : ControllerBase
{
    /// <summary>Returns an enum-bearing payload through the typed HTTP result pipeline.</summary>
    [HttpGet]
    public Ok<Payload> Get() => TypedResults.Ok(new Payload { Name = "example", Day = DayOfWeek.Monday });

    /// <summary>Validates and echoes a controller-bound JSON payload.</summary>
    [HttpPost]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Ok<Payload> Post([FromBody] Payload request) => TypedResults.Ok(request);

    /// <summary>Provides a deterministic validation and enum-serialization contract.</summary>
    public sealed record Payload
    {
        /// <summary>Gets the required synthetic name.</summary>
        /// <example>example</example>
        [Required]
        public string? Name { get; init; }

        /// <summary>Gets the day represented as a string on the JSON wire.</summary>
        /// <example>Monday</example>
        public DayOfWeek Day { get; init; }
    }
}

using CasCap.Tests.Infrastructure;
using System.Net;

namespace CasCap.Tests.Api;

/// <summary>
/// Integration tests for the application health-check endpoints exposed by CasCap.App.
/// </summary>
/// <remarks>
/// These tests boot the full application via <see cref="CasCapAppWebApplicationFactory"/>
/// and verify that the health-check middleware is correctly wired up.
/// <para>
/// The <c>/healthz</c> endpoint may return <see cref="HttpStatusCode.ServiceUnavailable"/>
/// (503) when Redis or Azure Storage are unreachable, but it must always respond rather than
/// throwing an unhandled exception.
/// </para>
/// </remarks>
public class HealthTests(ITestOutputHelper output) : WebApiTestBase
{
    /// <summary>
    /// The root health-check endpoint should always respond, even when external
    /// dependencies (Redis, Azure Storage) are unavailable.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task HealthEndpoint_Responds()
    {
        var response = await AnonymousClient.GetAsync("/healthz");

        // 200 OK  → all health checks passing
        // 503 Service Unavailable → one or more checks degraded (expected in CI without Redis)
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Expected 200 or 503 but got {(int)response.StatusCode} {response.StatusCode}");

        var body = await response.Content.ReadAsStringAsync();
        output.WriteLine($"GET /healthz → {(int)response.StatusCode}: {body}");
    }

    /// <summary>
    /// The liveness probe endpoint (<c>/healthz/live</c>) should always respond.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task LivenessProbe_Responds()
    {
        var response = await AnonymousClient.GetAsync("/healthz/live");

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Expected 200 or 503 but got {(int)response.StatusCode} {response.StatusCode}");

        output.WriteLine($"GET /healthz/live → {(int)response.StatusCode}");
    }

    /// <summary>
    /// The readiness probe endpoint (<c>/healthz/ready</c>) should always respond.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReadinessProbe_Responds()
    {
        var response = await AnonymousClient.GetAsync("/healthz/ready");

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Expected 200 or 503 but got {(int)response.StatusCode} {response.StatusCode}");

        output.WriteLine($"GET /healthz/ready → {(int)response.StatusCode}");
    }

    /// <summary>
    /// All health-check probe endpoints must be accessible without authentication.
    /// </summary>
    [Theory]
    [InlineData("/healthz")]
    [InlineData("/healthz/live")]
    [InlineData("/healthz/ready")]
    [InlineData("/healthz/startup")]
    [Trait("Category", "Integration")]
    public async Task AllProbeEndpoints_AllowAnonymous(string path)
    {
        // Use an anonymous client (no auth header) – probes must be AllowAnonymous
        var response = await AnonymousClient.GetAsync(path);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        output.WriteLine($"GET {path} → {(int)response.StatusCode} (anonymous)");
    }
}

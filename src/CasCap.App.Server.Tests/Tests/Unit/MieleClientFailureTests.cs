using CasCap.Exceptions;
using CasCap.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace CasCap.Tests.Unit;

/// <summary>Checks domain translation of real Polly exceptions through an offline HTTP handler.</summary>
[Trait("Category", "Device Commands")]
public sealed class MieleClientFailureTests
{
    [Theory]
    [InlineData("timeout", false)]
    [InlineData("timeout", true)]
    [InlineData("circuit", false)]
    [InlineData("circuit", true)]
    [InlineData("isolated-circuit", false)]
    [InlineData("isolated-circuit", true)]
    public async Task ResilienceFailures_MapToSanitizedDomainErrors(string failure, bool write)
    {
        // Regression: disabled retries do not disable Polly timeouts or circuit breakers.
        using var handler = new MieleHttpHandler();
        handler.Reset(failure: failure switch
        {
            "timeout" => new TimeoutRejectedException("Synthetic private provider detail."),
            "circuit" => new BrokenCircuitException("Synthetic private provider detail."),
            _ => new IsolatedCircuitException("Synthetic private provider detail.")
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
        var sut = CreateClient(client);

        var exception = await Assert.ThrowsAsync<MieleApiException>(() => write
            ? sut.PutAction("example", new() { PowerOn = true }, TestContext.Current.CancellationToken)
            : (Task)sut.GetDevices(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Null(exception.StatusCode);
        Assert.Equal(failure == "timeout"
            ? "The Miele API request timed out."
            : "The Miele API is temporarily unavailable.", exception.Message);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task RequestedCancellation_IsNotTranslated()
    {
        using var handler = new MieleHttpHandler();
        handler.Reset(block: true);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
        using var owner = new CancellationTokenSource();
        var running = CreateClient(client).GetDevices(cancellationToken: owner.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await owner.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            running.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.True(Assert.Single(handler.Calls).Token.IsCancellationRequested);
    }

    private static MieleClientService CreateClient(HttpClient client)
        => new(NullLogger<MieleClientService>.Instance, Options.Create(new MieleConfig
        {
            OAuthToken = "example",
            HealthCheckUri = "https://example.com/",
            HealthCheckExpectedHttpStatusCodes = [404],
            HealthCheck = KubernetesProbeTypes.None
        }), new TestClientFactory(client));

    private sealed class TestClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}

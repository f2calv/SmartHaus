using Microsoft.AspNetCore.Mvc.Testing;

namespace CasCap.Tests.Infrastructure;

/// <summary>
/// Abstract base class for integration tests that use <see cref="CasCapAppWebApplicationFactory"/>
/// to spin up an in-process test server.
/// </summary>
/// <remarks>
/// Inheriting tests receive a pre-configured <see cref="HttpClient"/> with Basic-auth
/// credentials and access to the application's <see cref="IServiceProvider"/> for
/// asserting DI registrations.
/// </remarks>
public abstract class WebApiTestBase : IAsyncLifetime
{
    /// <summary>The shared factory instance for this test class.</summary>
    protected CasCapAppWebApplicationFactory Factory { get; } = new();

    /// <summary>
    /// An <see cref="HttpClient"/> with the test Basic-auth header pre-applied.
    /// Suitable for calling <c>[Authorize]</c>-protected endpoints.
    /// </summary>
    protected HttpClient AuthorizedClient { get; private set; } = null!;

    /// <summary>
    /// An <see cref="HttpClient"/> with no authorization headers, for testing
    /// anonymous access paths (health checks, public endpoints).
    /// </summary>
    protected HttpClient AnonymousClient { get; private set; } = null!;

    /// <summary>
    /// The application's <see cref="IServiceProvider"/>, resolved after the host has
    /// started.  Use this to assert DI registrations without making HTTP calls.
    /// </summary>
    protected IServiceProvider Services => Factory.Services;

    /// <inheritdoc/>
    public virtual Task InitializeAsync()
    {
        AuthorizedClient = Factory.CreateAuthorizedClient();
        AnonymousClient = Factory.CreateAnonymousClient();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual async Task DisposeAsync()
    {
        AuthorizedClient.Dispose();
        AnonymousClient.Dispose();
        await Factory.DisposeAsync();
    }
}

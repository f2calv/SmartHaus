using CasCap.Common.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CasCap.Tests.Infrastructure;

/// <summary>
/// A <see cref="WebApplicationFactory{TProgram}"/> for CasCap.App, configured for
/// integration testing without requiring live infrastructure (Redis, Azure Key Vault,
/// signal-cli, etc.).
/// </summary>
/// <remarks>
/// <para>
/// The factory boots the full <c>Program</c> startup pipeline and then applies a set of
/// in-memory configuration overrides and service replacements that make the application
/// suitable for automated tests:
/// </para>
/// <list type="bullet">
///   <item>Environment is set to <c>"Testing"</c> so that <c>appsettings.Testing.json</c>
///         (if present) is loaded and the Key Vault bootstrap is skipped (no cert available).</item>
///   <item>Mandatory configuration values that normally come from Key Vault / user-secrets
///         (<c>EnabledFeatures</c>, <c>ApiAuthConfig</c>, <c>SignalCliConfig.BaseAddress</c>) are
///         provided via in-memory collection.</item>
///   <item>Authorization is replaced with a permissive policy so API endpoints can be
///         exercised without managing credentials in every test.
///         Use <see cref="CreateAnonymousClient"/> to explicitly test 401 responses.</item>
/// </list>
/// <para>
/// Tests that exercise endpoints backed by Redis, Azure Storage, or external HTTP services
/// (signal-cli, Buderus, KNX hardware…) must be decorated with
/// <c>[Fact(Skip = "Requires &lt;infrastructure&gt;")]</c> when running in a CI environment
/// where those dependencies are absent.
/// </para>
/// </remarks>
public class CasCapAppWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// The Basic-auth username injected into the test configuration.
    /// </summary>
    public const string TestUsername = "testuser";

    /// <summary>
    /// The Basic-auth password injected into the test configuration.
    /// </summary>
    public const string TestPassword = "testpass";

    /// <summary>
    /// Configuration key for the <c>EnabledFeatures</c> property.
    /// Matches the path that <see cref="FeatureConfig"/> binds to at runtime.
    /// </summary>
    public const string EnabledFeaturesConfigKey = "CasCap:FeatureConfig:EnabledFeatures";

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Boot the application in a "Testing" environment so:
        //   - appsettings.Testing.json is loaded (optional override file).
        //   - IsDevelopment() returns false, matching the production auth path so we
        //     can verify that the permissive override below works as expected.
        //   - Azure Key Vault bootstrap is skipped (no certificate available in CI).
        builder.UseEnvironment("Testing");

        // Supply required configuration values that are not present in the committed
        // appsettings.json and would normally come from Key Vault or user-secrets.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // ── FeatureConfig ──────────────────────────────────────────────────────
                // Set a minimal EnabledFeatures (Test only) so none of the hardware feature flags
                // (Knx, Buderus, DoorBird …) are activated, keeping startup fast and
                // independent of external hardware.
                [EnabledFeaturesConfigKey]
                    = FeatureNames.Test,

                // ── ApiAuthConfig ──────────────────────────────────────────────────────
                [$"{ApiAuthConfig.ConfigurationSectionName}:Username"] = TestUsername,
                [$"{ApiAuthConfig.ConfigurationSectionName}:Password"] = TestPassword,

                // ── SignalCliConfig ────────────────────────────────────────────────────
                // AddNotifications() is always called in Program.cs and requires a valid
                // BaseAddress. Point to localhost – the service will fail gracefully when
                // it cannot reach signal-cli.
                [$"{SignalCliConfig.ConfigurationSectionName}:BaseAddress"]
                    = "http://localhost:9922",

                // ── CachingConfig ──────────────────────────────────────────────────────
                // Use abortConnect=false so StackExchange.Redis does not throw during
                // startup if local Redis is unavailable.
                [$"{CachingConfig.ConfigurationSectionName}:{nameof(CachingConfig.RemoteCacheConnectionString)}"]
                    = "localhost:6379,abortConnect=false",
                [$"{CachingConfig.ConfigurationSectionName}:{nameof(CachingConfig.DistributedLockingEnabled)}"]
                    = "true",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the default authorization policy with one that always succeeds.
            // This mirrors what Program.cs does in IsDevelopment() and lets tests hit
            // protected endpoints without managing credentials.
            // Individual tests that want to verify 401 behaviour should use
            // CreateAnonymousClient() instead.
            services.AddAuthorizationBuilder()
                .SetDefaultPolicy(
                    new AuthorizationPolicyBuilder()
                        .RequireAssertion(_ => true)
                        .Build());
        });
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> with the test Basic-auth credentials pre-applied,
    /// suitable for calling <c>[Authorize]</c>-protected endpoints.
    /// </summary>
    public HttpClient CreateAuthorizedClient()
    {
        var client = CreateClient();
        var credentials = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{TestUsername}:{TestPassword}"));
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                BasicAuthenticationHandler.SchemeName, credentials);
        return client;
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> with no authorization headers, suitable for
    /// testing anonymous access paths (e.g., health-check endpoints) or verifying that
    /// protected endpoints return <c>401 Unauthorized</c> when the permissive default
    /// policy is NOT applied.
    /// </summary>
    public HttpClient CreateAnonymousClient() => CreateClient();
}

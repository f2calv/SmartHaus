using CasCap.Common.Authentication;
using CasCap.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CasCap.Tests.Api;

/// <summary>
/// Integration tests for the <c>SystemController</c>.
/// </summary>
/// <remarks>
/// <c>SystemController</c> has a single <c>GET /api/system</c> endpoint decorated
/// with <c>[Authorize]</c>.  In the testing environment the default authorization policy
/// is replaced with a permissive policy (see <see cref="CasCapAppWebApplicationFactory"/>),
/// so <see cref="WebApiTestBase.AuthorizedClient"/> and <see cref="WebApiTestBase.AnonymousClient"/>
/// both succeed.
/// The explicit-credentials tests exercise the real Basic authentication handler
/// by temporarily restoring credential checking.
/// </remarks>
public class SystemControllerTests(ITestOutputHelper output) : WebApiTestBase
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// <c>GET /api/system</c> returns 200 and an <see cref="AppConfig"/> payload when
    /// called with the authorized client.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetSystem_WithAuthorizedClient_Returns200AndAppConfig()
    {
        var response = await AuthorizedClient.GetAsync("/api/system");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        output.WriteLine($"GET /api/system → {json[..Math.Min(200, json.Length)]}");

        // The returned JSON must at minimum contain the EnabledFeatures field.
        var doc = JsonDocument.Parse(json);
        Assert.True(
            doc.RootElement.TryGetProperty("EnabledFeatures", out _) ||
            doc.RootElement.TryGetProperty("EnabledFeatures", out _),
            "Response JSON should contain 'EnabledFeatures' property");
    }

    /// <summary>
    /// The <c>EnabledFeatures</c> returned by <c>GET /api/system</c> should match the value
    /// injected by the test factory (<c>Api</c>).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetSystem_EnabledFeatures_MatchesTestConfiguration()
    {
        var response = await AuthorizedClient.GetAsync("/api/system");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        // EnabledFeatures is a comma-separated string (e.g. "Api").
        var EnabledFeaturesElement = doc.RootElement.TryGetProperty("EnabledFeatures", out var el)
            ? el
            : doc.RootElement.GetProperty("EnabledFeatures");

        output.WriteLine($"EnabledFeatures value in response: {EnabledFeaturesElement}");

        var isApiMode =
            EnabledFeaturesElement.ValueKind == JsonValueKind.String &&
            EnabledFeaturesElement.GetString() == FeatureNames.Test;

        Assert.True(isApiMode, $"Expected EnabledFeatures to be '{FeatureNames.Test}' but was '{EnabledFeaturesElement}'");
    }

    /// <summary>
    /// Verifies that the Basic authentication handler rejects requests with
    /// wrong credentials when the factory is used without the permissive authorization
    /// override – demonstrated by creating a client against a factory instance that has
    /// the default (non-permissive) authorization policy.
    /// </summary>
    /// <remarks>
    /// This test creates a second, non-permissive factory that restores the standard
    /// <c>[Authorize]</c> behaviour.  Because <see cref="CasCapAppWebApplicationFactory"/>
    /// replaces the default policy with a permissive one for convenience, this scenario
    /// is tested via a separate, stricter factory subclass.
    /// </remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetSystem_WithWrongCredentials_Returns401()
    {
        await using var strictFactory = new StrictAuthCasCapAppWebApplicationFactory();
        var client = strictFactory.CreateClient();

        var wrongCredentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("baduser:badpass"));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", wrongCredentials);

        var response = await client.GetAsync("/api/system");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        output.WriteLine($"GET /api/system (wrong creds) → {(int)response.StatusCode}");
    }

    /// <summary>
    /// Verifies that the Basic authentication handler accepts correct credentials.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetSystem_WithCorrectCredentials_Returns200()
    {
        await using var strictFactory = new StrictAuthCasCapAppWebApplicationFactory();
        var client = strictFactory.CreateAuthorizedClient();

        var response = await client.GetAsync("/api/system");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        output.WriteLine($"GET /api/system (correct creds) → {(int)response.StatusCode}");
    }

    /// <summary>
    /// A variant of <see cref="CasCapAppWebApplicationFactory"/> that does NOT replace the
    /// default authorization policy, enabling tests that verify real 401 / 403 behaviour.
    /// </summary>
    private sealed class StrictAuthCasCapAppWebApplicationFactory : CasCapAppWebApplicationFactory
    {
        /// <inheritdoc/>
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            // Apply all the base overrides (config, environment …) but skip the permissive
            // authorization replacement added in CasCapAppWebApplicationFactory.ConfigureWebHost.
            // We do this by calling base first and then re-registering a strict policy.
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.AddAuthorizationBuilder()
                    .SetDefaultPolicy(
                        new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                            .AddAuthenticationSchemes(BasicAuthenticationHandler.SchemeName)
                            .RequireAuthenticatedUser()
                            .Build());
            });
        }
    }
}

using CasCap.Common.Authentication;
using CasCap.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

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
    /// <summary>
    /// <c>GET /api/system</c> returns 200 and a <see cref="GitMetadata"/> payload when
    /// called with the authorized client.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetSystem_WithAuthorizedClient_Returns200AndGitMetadata()
    {
        var response = await AuthorizedClient.GetAsync("/api/system", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        output.WriteLine($"GET /api/system → {json[..Math.Min(200, json.Length)]}");

        var metadata = JsonSerializer.Deserialize<GitMetadata>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(metadata);
    }

    /// <summary>
    /// The metadata returned by <c>GET /api/system</c> matches the registered singleton.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetSystem_ReturnsRegisteredGitMetadata()
    {
        var response = await AuthorizedClient.GetAsync("/api/system", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var actual = JsonSerializer.Deserialize<GitMetadata>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var expected = Services.GetRequiredService<GitMetadata>();

        Assert.NotNull(actual);
        Assert.Equal(expected.GIT_REPOSITORY, actual.GIT_REPOSITORY);
        Assert.Equal(expected.GIT_BRANCH, actual.GIT_BRANCH);
        Assert.Equal(expected.GIT_COMMIT, actual.GIT_COMMIT);
        Assert.Equal(expected.GIT_TAG, actual.GIT_TAG);
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

        var response = await client.GetAsync("/api/system", TestContext.Current.CancellationToken);

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

        var response = await client.GetAsync("/api/system", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        output.WriteLine($"GET /api/system (correct creds) → {(int)response.StatusCode}");
    }

}

using CasCap.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace CasCap.Tests.Api;

/// <summary>Integration tests for Basic authentication on the MCP endpoint.</summary>
public sealed class McpAuthenticationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Mcp_WithAnonymousClient_Returns401()
    {
        await using var factory = new StrictAuthCasCapAppWebApplicationFactory();
        using var client = factory.CreateAnonymousClient();

        using var response = await client.SendAsync(CreateInitializeRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Mcp_WithAuthorizedClient_ReachesProtocolEndpoint()
    {
        await using var factory = new StrictAuthCasCapAppWebApplicationFactory();
        using var client = factory.CreateAuthorizedClient();

        using var response = await client.SendAsync(CreateInitializeRequest(), TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static HttpRequestMessage CreateInitializeRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2025-06-18",
                    capabilities = new { },
                    clientInfo = new { name = "integration-test", version = "1.0" },
                },
            }),
        };
        request.Headers.Accept.ParseAdd("application/json, text/event-stream");
        return request;
    }
}
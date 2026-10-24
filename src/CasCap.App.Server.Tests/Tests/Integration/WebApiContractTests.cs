using CasCap.Tests.Infrastructure;
using System.Net;
using System.Text;

namespace CasCap.Tests;

/// <summary>Checks typed JSON, MVC input validation and generated OpenAPI through the real host.</summary>
[Trait("Category", "Integration")]
public sealed class WebApiContractTests(ApiContractWebApplicationFactory factory)
    : IClassFixture<ApiContractWebApplicationFactory>
{
    [Fact]
    public async Task TypedResults_EmitStringEnums()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/contract-probe", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Monday", body.RootElement.GetProperty("day").GetString());
        Assert.Equal("example", body.RootElement.GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("{\"name\":\"example\",\"day\":\"Friday\"}", "Friday")]
    [InlineData("{\"name\":\"example\",\"day\":\"Monday\"}", "Monday")]
    public async Task ControllerBinding_AcceptsStringEnums(string json, string expectedDay)
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/v1/contract-probe", content, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(expectedDay, body.RootElement.GetProperty("day").GetString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{")]
    [InlineData("")]
    [InlineData("{\"name\":\"example\",\"day\":\"NotADay\"}")]
    public async Task InvalidRequests_ReturnValidationProblem(string json)
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/v1/contract-probe", content, TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
        Assert.NotEmpty(body.RootElement.GetProperty("errors").EnumerateObject());
    }

    [Fact]
    public async Task OpenApi_DescribesStringEnumsAndTypedResponses()
    {
        using var client = factory.CreateAuthorizedClient();
        using var response = await client.GetAsync("/swagger/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var operation = body.RootElement.GetProperty("paths").GetProperty("/api/v1/contract-probe");
        Assert.True(operation.GetProperty("get").GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(operation.GetProperty("post").GetProperty("responses").TryGetProperty("400", out _));
        var schemas = body.RootElement.GetProperty("components").GetProperty("schemas");
        var day = schemas.GetProperty("DayOfWeek");
        Assert.Equal("string", day.GetProperty("type").GetString());
        Assert.Contains(day.GetProperty("enum").EnumerateArray(), value => value.GetString() == "Monday");
    }
}

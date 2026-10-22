using CasCap.Models.Dtos;
using CasCap.Services;
using CasCap.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;

namespace CasCap.Tests;

/// <summary>Checks appliance HTTP contracts against a fake provider, never live appliances.</summary>
[Trait("Category", "Integration")]
public sealed class MieleApiContractTests(MieleApiWebApplicationFactory factory) : IClassFixture<MieleApiWebApplicationFactory>
{
    [Theory]
    [InlineData("devices", "{}")]
    [InlineData("devices/short", "[]")]
    [InlineData("devices/example", "{\"ident\":null,\"state\":null}")]
    [InlineData("devices/example/ident", "{\"deviceName\":\"Example appliance\"}")]
    [InlineData("devices/example/state", "{\"signalInfo\":false}")]
    [InlineData("devices/example/actions", "{\"processAction\":[1,2]}")]
    [InlineData("devices/example/programs", "[]")]
    public async Task Reads_PreserveSuccessfulJsonAndDistinguishMissingAndFailed(string route, string json)
    {
        using var client = factory.CreateClient();
        foreach (var status in new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.ServiceUnavailable })
        {
            factory.Provider.Reset(status, json);
            using var response = await client.GetAsync($"/api/v1/Miele/{route}", TestContext.Current.CancellationToken);

            Assert.Equal(status == HttpStatusCode.ServiceUnavailable ? HttpStatusCode.BadGateway : status, response.StatusCode);
            Assert.Single(factory.Provider.Calls);
            Assert.True(factory.Provider.ResponseDisposed);
            if (status == HttpStatusCode.OK)
            {
                using var expected = JsonDocument.Parse(json);
                using var actual = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                Assert.Equal(expected.RootElement.ValueKind, actual.RootElement.ValueKind);
                if (expected.RootElement.ValueKind == JsonValueKind.Array)
                    Assert.Empty(actual.RootElement.EnumerateArray());
                else if (route == "devices")
                    Assert.Empty(actual.RootElement.EnumerateObject());
                else
                    foreach (var property in expected.RootElement.EnumerateObject())
                        Assert.True(JsonElement.DeepEquals(property.Value, actual.RootElement.GetProperty(property.Name)), property.Name);
            }
            if (status == HttpStatusCode.ServiceUnavailable)
                await AssertGatewayProblem(response);
        }
    }

    [Theory]
    [InlineData("actions", "{\"powerOn\":false}")]
    [InlineData("programs", "{\"programId\":24,\"duration\":[1,15],\"temperature\":220}")]
    public async Task Commands_MapProviderOutcomesWithoutReplaying(string route, string json)
    {
        using var client = factory.CreateClient();
        foreach (var status in new[]
        {
            HttpStatusCode.NoContent, HttpStatusCode.BadRequest, HttpStatusCode.MethodNotAllowed, HttpStatusCode.Conflict,
            HttpStatusCode.UnprocessableEntity, HttpStatusCode.NotFound, HttpStatusCode.Unauthorized, HttpStatusCode.ServiceUnavailable,
        })
        {
            factory.Provider.Reset(status);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await client.PutAsync($"/api/v1/Miele/devices/example/{route}", content, TestContext.Current.CancellationToken);
            var expected = status switch
            {
                HttpStatusCode.NoContent => HttpStatusCode.OK,
                HttpStatusCode.NotFound => HttpStatusCode.NotFound,
                HttpStatusCode.Unauthorized or HttpStatusCode.ServiceUnavailable => HttpStatusCode.BadGateway,
                _ => HttpStatusCode.BadRequest,
            };

            Assert.Equal(expected, response.StatusCode);
            var call = Assert.Single(factory.Provider.Calls);
            Assert.True(factory.Provider.ResponseDisposed);
            if (expected == HttpStatusCode.OK)
            {
                Assert.Equal("true", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                Assert.Equal(json, call.Body);
            }
            if (expected == HttpStatusCode.BadGateway)
                await AssertGatewayProblem(response);
        }
    }

    [Theory]
    [InlineData("actions", "{}")]
    [InlineData("actions", "{\"processAction\":0}")]
    [InlineData("actions", "{\"processAction\":8}")]
    [InlineData("actions", "{\"light\":0}")]
    [InlineData("actions", "{\"ambientLight\":3}")]
    [InlineData("actions", "{\"modes\":4}")]
    [InlineData("actions", "{\"ventilationStep\":5}")]
    [InlineData("actions", "{\"startTime\":[1]}")]
    [InlineData("actions", "{\"startTime\":[-1,0]}")]
    [InlineData("actions", "{\"startTime\":[1,60]}")]
    [InlineData("actions", "{\"targetTemperature\":[{\"zone\":2}]}")]
    [InlineData("actions", "{\"targetTemperature\":[{\"value\":-20}]}")]
    [InlineData("actions", "{\"targetTemperature\":[]}")]
    [InlineData("actions", "{\"targetTemperature\":[null]}")]
    [InlineData("actions", "{\"runOnTime\":5}")]
    [InlineData("actions", "{\"runOnTime\":[0,5]}")]
    [InlineData("actions", "{\"powerOn\":true,\"unknown\":1}")]
    [InlineData("programs", "{}")]
    [InlineData("programs", "{\"programId\":null}")]
    [InlineData("programs", "{\"programId\":24,\"duration\":[1]}")]
    [InlineData("programs", "{\"programId\":24,\"duration\":[1,2,3]}")]
    [InlineData("programs", "{\"programId\":24,\"duration\":[0,60]}")]
    public async Task InvalidCommands_DoNotReachProvider(string route, string json)
    {
        using var client = factory.CreateClient();
        factory.Provider.Reset();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PutAsync($"/api/v1/Miele/devices/example/{route}", content, TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(body.RootElement.GetProperty("errors").EnumerateObject());
        Assert.Empty(factory.Provider.Calls);
    }

    [Theory]
    [InlineData("actions", "{\"processAction\":7,\"light\":2,\"ambientLight\":1,\"colors\":\"blue\",\"deviceName\":\"Example appliance\",\"modes\":0,\"powerOn\":false,\"powerOff\":true,\"programId\":4,\"startTime\":[0,59],\"targetTemperature\":[{\"zone\":2,\"value\":-20}],\"ventilationStep\":4}")]
    [InlineData("programs", "{\"programId\":0}")]
    [InlineData("programs", "{\"programId\":24,\"duration\":[0,0],\"temperature\":-20}")]
    public async Task ValidCommands_PreserveSupportedValues(string route, string json)
    {
        using var client = factory.CreateClient();
        factory.Provider.Reset(HttpStatusCode.NoContent);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PutAsync($"/api/v1/Miele/devices/example/{route}", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var call = Assert.Single(factory.Provider.Calls);
        using var expected = JsonDocument.Parse(json);
        using var actual = JsonDocument.Parse(Assert.IsType<string>(call.Body));
        Assert.True(JsonElement.DeepEquals(expected.RootElement, actual.RootElement));
    }

    [Theory]
    [InlineData(false, "{}")]
    [InlineData(false, "{\"processAction\":8}")]
    [InlineData(false, "{\"startTime\":[0,60]}")]
    [InlineData(false, "{\"targetTemperature\":[null]}")]
    [InlineData(true, "{}")]
    [InlineData(true, "{\"programId\":24,\"duration\":[-1,0]}")]
    public async Task DirectClientValidation(bool program, string json)
    {
        var client = factory.Services.GetRequiredService<MieleClientService>();
        factory.Provider.Reset();
        var options = JsonSerializerOptions.Web;

        await Assert.ThrowsAsync<ValidationException>(() => program
            ? client.PutProgram("example", JsonSerializer.Deserialize<MieleProgramRequest>(json, options)!, TestContext.Current.CancellationToken)
            : client.PutAction("example", JsonSerializer.Deserialize<MieleActionRequest>(json, options)!, TestContext.Current.CancellationToken));

        Assert.Empty(factory.Provider.Calls);
    }

    [Fact]
    public async Task ProgrammingFailuresAreNotProviderFailures()
    {
        var client = factory.Services.GetRequiredService<MieleClientService>();
        var failure = new InvalidOperationException("Synthetic programming fault.");
        factory.Provider.Reset(failure: failure);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetDevices(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(failure, actual);
        Assert.Single(factory.Provider.Calls);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/plain")]
    [InlineData("text/html")]
    public async Task GatewayProblemAcceptHeaders(string accept)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        factory.Provider.Reset(HttpStatusCode.ServiceUnavailable);
        using var response = await client.GetAsync("/api/v1/Miele/devices", TestContext.Current.CancellationToken);

        await AssertGatewayProblem(response);
    }

    [Theory]
    [InlineData("GET", "devices", null)]
    [InlineData("GET", "devices/short", null)]
    [InlineData("GET", "devices/example", null)]
    [InlineData("GET", "devices/example/ident", null)]
    [InlineData("GET", "devices/example/state", null)]
    [InlineData("GET", "devices/example/actions", null)]
    [InlineData("GET", "devices/example/programs", null)]
    [InlineData("PUT", "devices/example/actions", "{\"powerOn\":true}")]
    [InlineData("PUT", "devices/example/programs", "{\"programId\":24}")]
    public async Task RequestCancellation_StopsTheProviderRequest(string method, string route, string? json)
    {
        using var client = factory.CreateClient();
        factory.Provider.Reset(block: true);
        using var stopping = new CancellationTokenSource();
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/v1/Miele/{route}");
        if (json is not null)
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        var task = client.SendAsync(request, stopping.Token);
        await factory.Provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await stopping.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        await factory.Provider.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Single(factory.Provider.Calls);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("")]
    public async Task InvalidProviderResponse_ReturnsGatewayProblem(string json)
    {
        using var client = factory.CreateClient();
        factory.Provider.Reset(json: json);
        using var response = await client.GetAsync("/api/v1/Miele/devices", TestContext.Current.CancellationToken);

        await AssertGatewayProblem(response);
        Assert.True(factory.Provider.ResponseDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransportFailures_ReturnSanitizedGatewayProblem(bool timeout)
    {
        using var client = factory.CreateClient();
        Exception failure = timeout ? new TaskCanceledException("Synthetic private provider detail.") : new HttpRequestException("Synthetic private provider detail.");
        factory.Provider.Reset(failure: failure);
        using var response = await client.GetAsync("/api/v1/Miele/devices", TestContext.Current.CancellationToken);

        await AssertGatewayProblem(response);
    }

    [Fact]
    public async Task OpenApi_DescribesTypedCommandsAndErrorOutcomes()
    {
        using var client = factory.CreateAuthorizedClient();
        using var response = await client.GetAsync("/swagger/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = body.RootElement.GetProperty("paths");
        foreach (var (route, schema) in new[] { ("actions", "MieleActionRequest"), ("programs", "MieleProgramRequest") })
        {
            var path = Assert.Single(paths.EnumerateObject(), property =>
                property.Name.Equals($"/api/v1/Miele/devices/{{deviceId}}/{route}", StringComparison.OrdinalIgnoreCase)).Value.GetProperty("put");
            Assert.EndsWith($"/{schema}", path.GetProperty("requestBody").GetProperty("content")
                .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
            foreach (var status in new[] { "200", "400", "404", "502" })
                Assert.True(path.GetProperty("responses").TryGetProperty(status, out _));
        }
    }

    private static async Task AssertGatewayProblem(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(502, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Miele API unavailable", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("The appliance provider could not complete the request.", body.RootElement.GetProperty("detail").GetString());
    }
}

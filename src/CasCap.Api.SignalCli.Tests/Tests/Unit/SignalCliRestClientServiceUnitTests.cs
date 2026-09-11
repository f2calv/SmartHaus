using System.Net;

namespace CasCap.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="SignalCliRestClientService"/> that run against a stubbed
/// <see cref="HttpMessageHandler"/>. They need no signal-cli server, no Azure Key Vault and no
/// credentials, so the suite is green on a clean clone.
/// </summary>
[Trait("Category", "Unit")]
public class SignalCliRestClientServiceUnitTests(ITestOutputHelper output)
{
    private const string BaseAddress = "http://localhost:8080";
    private const string Account = "+441234567890";

    #region Request shaping

    [Fact]
    public async Task GetAbout_DeserializesResponse()
    {
        var handler = StubHttpMessageHandler.RespondJson("""
            {"build":2,"version":"0.98","versions":["v1","v2"],"mode":"json-rpc"}
            """);
        var svc = CreateService(handler);

        var about = await svc.GetAbout(TestContext.Current.CancellationToken);

        Assert.NotNull(about);
        Assert.Equal(2, about.Build);
        Assert.Equal("0.98", about.Version);
        Assert.Equal(["v1", "v2"], about.Versions);
        Assert.Equal("/v1/about", Assert.Single(handler.Calls).PathAndQuery);
    }

    [Fact]
    public async Task SendMessage_PostsToV2SendAndReturnsTimestamp()
    {
        var handler = StubHttpMessageHandler.RespondJson("""{"timestamp":"1712153610000"}""");
        var svc = CreateService(handler);

        var response = await svc.SendMessage(new SignalMessageRequest
        {
            Number = Account,
            Recipients = ["+449876543210"],
            Message = "hello"
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Equal("1712153610000", response.Timestamp);

        var call = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.Equal("/v2/send", call.PathAndQuery);
        Assert.Contains("\"message\":\"hello\"", call.Body);
        //The serializer escapes '+' as \u002B, so match on the digits rather than the literal number.
        Assert.Contains("\"recipients\":", call.Body);
        Assert.Contains("449876543210", call.Body);
    }

    [Fact]
    public async Task ReceiveMessages_EscapesThePhoneNumberInThePath()
    {
        var handler = StubHttpMessageHandler.RespondJson("[]");
        var svc = CreateService(handler);

        await svc.ReceiveMessages(Account, TestContext.Current.CancellationToken);

        Assert.Equal("/v1/receive/%2B441234567890", Assert.Single(handler.Calls).PathAndQuery);
    }

    [Fact]
    public async Task SendReaction_SerializesSnakeCasePayload()
    {
        var handler = StubHttpMessageHandler.RespondStatus(HttpStatusCode.NoContent);
        var svc = CreateService(handler);

        var sent = await svc.SendReaction(Account, "+449876543210", "\U0001F44D", Account, 1712153610000,
            TestContext.Current.CancellationToken);

        Assert.True(sent);
        var call = Assert.Single(handler.Calls);
        Assert.Equal("/v1/reactions/%2B441234567890", call.PathAndQuery);
        Assert.Contains("target_author", call.Body);
    }

    [Fact]
    public async Task SearchNumbers_RepeatsTheNumbersQueryParameter()
    {
        var handler = StubHttpMessageHandler.RespondJson("[]");
        var svc = CreateService(handler);

        await svc.SearchNumbers(Account, ["+4411111", "+4422222"], TestContext.Current.CancellationToken);

        var call = Assert.Single(handler.Calls);
        Assert.Contains("numbers=%2B4411111", call.PathAndQuery);
        Assert.Contains("numbers=%2B4422222", call.PathAndQuery);
    }

    [Fact]
    public async Task DeleteAttachment_IssuesDelete()
    {
        var handler = StubHttpMessageHandler.RespondStatus(HttpStatusCode.NoContent);
        var svc = CreateService(handler);

        var deleted = await svc.DeleteAttachment("attachment-1", TestContext.Current.CancellationToken);

        Assert.True(deleted);
        var call = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Delete, call.Method);
        Assert.Equal("/v1/attachments/attachment-1", call.PathAndQuery);
    }

    [Fact]
    public async Task ListGroups_DeserializesArray()
    {
        var handler = StubHttpMessageHandler.RespondJson("""
            [{"id":"group.abc","name":"Haus","members":["+441234567890"]}]
            """);
        var svc = CreateService(handler);

        var groups = await svc.ListGroups(Account, TestContext.Current.CancellationToken);

        var group = Assert.Single(groups!);
        Assert.Equal("group.abc", group.Id);
        Assert.Equal("Haus", group.Name);
    }

    #endregion

    #region Failure handling

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task GetAbout_ReturnsNullOnFailureStatus(HttpStatusCode statusCode)
    {
        var handler = StubHttpMessageHandler.RespondStatus(statusCode);
        var svc = CreateService(handler);

        var about = await svc.GetAbout(TestContext.Current.CancellationToken);

        Assert.Null(about);
        output.WriteLine($"{statusCode} produced a null result rather than an exception");
    }

    [Fact]
    public async Task SetPin_ReturnsFalseOnFailureStatus()
    {
        var handler = StubHttpMessageHandler.RespondStatus(HttpStatusCode.BadRequest);
        var svc = CreateService(handler);

        Assert.False(await svc.SetPin(Account, "1234", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAbout_WhenTransportThrows_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var svc = CreateService(handler);

        Assert.Null(await svc.GetAbout(TestContext.Current.CancellationToken));
    }

    #endregion

    #region Cancellation

    [Fact]
    public async Task GetAbout_WithCancelledToken_Throws()
    {
        var handler = StubHttpMessageHandler.RespondJson("""{"build":2,"version":"0.98"}""");
        var svc = CreateService(handler);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => svc.GetAbout(cts.Token));
    }

    [Fact]
    public async Task SetPin_WithCancelledToken_ThrowsRatherThanReturningFalse()
    {
        var handler = StubHttpMessageHandler.RespondStatus(HttpStatusCode.NoContent);
        var svc = CreateService(handler);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => svc.SetPin(Account, "1234", cts.Token));
    }

    #endregion

    #region Streaming

    [Fact]
    public async Task StreamMessagesAsync_YieldsMessagesFromSuccessivePolls()
    {
        var handler = StubHttpMessageHandler.RespondJson("""
            [{"envelope":{"source":"+449876543210","timestamp":1,"dataMessage":{"message":"one","timestamp":1}}}]
            """);
        var svc = CreateService(handler, pollIntervalMs: 1);

        var received = new List<SignalReceivedMessage>();
        await foreach (var message in svc.StreamMessagesAsync(TestContext.Current.CancellationToken))
        {
            received.Add(message);
            if (received.Count == 3)
                break;
        }

        Assert.Equal(3, received.Count);
        Assert.All(received, m => Assert.Equal("one", m.Envelope.DataMessage?.Message));
        output.WriteLine($"Abandoning the enumerator stopped polling after {handler.Calls.Count} request(s)");
    }

    [Fact]
    public async Task StreamMessagesAsync_WhenCancelled_Throws()
    {
        var handler = StubHttpMessageHandler.RespondJson("[]");
        var svc = CreateService(handler, pollIntervalMs: 1);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in svc.StreamMessagesAsync(cts.Token))
            {
                //The stub yields nothing, so the stream ends only when the token is cancelled.
            }
        });
    }

    #endregion

    #region Private helpers

    private static SignalCliRestClientService CreateService(StubHttpMessageHandler handler, int pollIntervalMs = 1_000)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri(BaseAddress) };
        var config = new SignalCliConfig
        {
            BaseAddress = BaseAddress,
            PhoneNumber = Account,
            ReceivePollIntervalMs = pollIntervalMs
        };
        return new SignalCliRestClientService(
            NullLogger<SignalCliRestClientService>.Instance,
            Options.Create(config),
            new StubHttpClientFactory(client));
    }

    #endregion
}

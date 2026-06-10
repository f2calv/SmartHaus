namespace CasCap.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="SignalCliJsonRpcClientService"/> against a real signal-cli REST API
/// instance running in <c>json-rpc</c> mode.
/// </summary>
[Trait("Category", "Integration")]
public class SignalCliJsonRpcClientServiceTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public void Transport_DefaultsToJsonRpc()
    {
        Assert.Equal(SignalCliTransport.JsonRpc, _config.TransportMode);
    }

    [Fact(Skip = "Requires signal-cli REST API running in json-rpc mode")]
    public async Task ConnectAsync_EstablishesWebSocket()
    {
        await using var jsonRpcSvc = CreateJsonRpcService();
        await jsonRpcSvc.ConnectAsync();
        _output.WriteLine("WebSocket connected successfully");
    }

    [Fact(Skip = "Requires signal-cli REST API running in json-rpc mode")]
    public async Task ReceiveAsync_ReturnsBufferedMessages()
    {
        await using var jsonRpcSvc = CreateJsonRpcService();
        var notifier = (INotifier)jsonRpcSvc;

        // Send a message to self so we have something to receive
        var msg = new SignalMessageRequest
        {
            Message = $"JSON-RPC test at {DateTime.UtcNow:O}",
            Number = _config.PhoneNumber,
            Recipients = [_config.PhoneNumber]
        };
        var sendResult = await notifier.SendAsync(msg);
        Assert.NotNull(sendResult);
        _output.WriteLine($"Sent message, timestamp={sendResult.Timestamp}");

        // Allow time for WebSocket delivery
        await Task.Delay(2000);

        var messages = await notifier.ReceiveAsync(_config.PhoneNumber);
        _output.WriteLine($"Received {messages?.Length ?? 0} message(s)");
        Assert.NotNull(messages);
    }

    [Fact(Skip = "Requires signal-cli REST API running in json-rpc mode")]
    public async Task ListGroupsAsync_DelegatesToRestClient()
    {
        await using var jsonRpcSvc = CreateJsonRpcService();
        var notifier = (INotifier)jsonRpcSvc;

        var groups = await notifier.ListGroupsAsync(_config.PhoneNumber);
        Assert.NotNull(groups);
        _output.WriteLine($"Groups={groups.Length}");
    }

    #region Private helpers

    private SignalCliJsonRpcClientService CreateJsonRpcService() =>
        new(
            _serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<SignalCliJsonRpcClientService>(),
            _serviceProvider.GetRequiredService<IOptions<SignalCliConfig>>(),
            _serviceProvider.GetRequiredService<SignalCliRestClientService>());

    #endregion
}

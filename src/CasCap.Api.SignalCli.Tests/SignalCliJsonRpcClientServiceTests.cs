using System.Net.WebSockets;

namespace CasCap.Tests;

/// <summary>
/// Tests for <see cref="SignalCliJsonRpcClientService"/> covering WebSocket URI construction,
/// buffer draining, and integration against a real signal-cli REST API instance running
/// in <c>json-rpc</c> mode.
/// </summary>
public class SignalCliJsonRpcClientServiceTests(ITestOutputHelper output) : TestBase(output)
{
    #region Unit tests

    [Theory]
    [InlineData("http://localhost:8080", "+49123456789", "ws://localhost:8080/v1/receive/%2B49123456789")]
    [InlineData("https://signal.example.com", "+44780143", "wss://signal.example.com/v1/receive/%2B44780143")]
    [InlineData("http://signalcli.svc.local:8080", "+1234", "ws://signalcli.svc.local:8080/v1/receive/%2B1234")]
    public void BuildWebSocketUri_ConstructsCorrectUri(string baseAddress, string phoneNumber, string expectedUri)
    {
        var uri = SignalCliJsonRpcClientService.BuildWebSocketUri(baseAddress, phoneNumber);
        _output.WriteLine($"Input=({baseAddress}, {phoneNumber}) => {uri}");
        Assert.Equal(expectedUri, uri.ToString());
    }

    [Fact]
    public void Transport_DefaultsToJsonRpc()
    {
        Assert.Equal(SignalCliTransport.JsonRpc, _config.TransportMode);
    }

    [Fact]
    public void SignalCliTransport_HasExpectedMembers()
    {
        var values = Enum.GetValues<SignalCliTransport>();
        Assert.Equal(4, values.Length);
        Assert.Contains(SignalCliTransport.Normal, values);
        Assert.Contains(SignalCliTransport.Native, values);
        Assert.Contains(SignalCliTransport.JsonRpc, values);
        Assert.Contains(SignalCliTransport.JsonRpcNative, values);
    }

    [Fact]
    public void JsonRpcNotification_WithDataMessage_DeserializesCorrectly()
    {
        const string json = """
            {
              "jsonrpc": "2.0",
              "method": "receive",
              "params": {
                "envelope": {
                  "source": "+49123456789",
                  "sourceNumber": "+49123456789",
                  "timestamp": 1712153610000,
                  "dataMessage": {
                    "message": "Hello from group",
                    "timestamp": 1712153610000,
                    "groupInfo": { "groupId": "abc123==" }
                  }
                },
                "account": "+49123456789"
              }
            }
            """;

        var notification = System.Text.Json.JsonSerializer.Deserialize<SignalCliJsonRpcNotification>(json);

        Assert.NotNull(notification);
        Assert.Equal("2.0", notification.JsonRpc);
        Assert.Equal("receive", notification.Method);
        Assert.NotNull(notification.Params);
        Assert.Equal("+49123456789", notification.Params.Envelope.Source);
        Assert.Equal("+49123456789", notification.Params.Account);
        Assert.True(((IReceivedNotification)notification.Params).HasContent);
        Assert.Equal("Hello from group", ((IReceivedNotification)notification.Params).Message);
        Assert.Equal("abc123==", ((IReceivedNotification)notification.Params).GroupId);
    }

    [Fact]
    public void JsonRpcNotification_WithNullParams_DoesNotThrow()
    {
        const string json = """{"jsonrpc":"2.0","method":"receive"}""";

        var notification = System.Text.Json.JsonSerializer.Deserialize<SignalCliJsonRpcNotification>(json);

        Assert.NotNull(notification);
        Assert.Null(notification.Params);
    }

    #endregion

    #region DI registration tests

    [Fact]
    public void AddSignalCli_WithRestTransport_RegistersRestClient()
    {
        var configuration = BuildConfiguration(SignalCliTransport.Normal);
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddXUnitLogging(_output);
        services.AddSignalCli(configuration, isDevelopment: false);

        using var sp = services.BuildServiceProvider();
        var notifier = sp.GetRequiredService<INotifier>();
        Assert.IsType<SignalCliRestClientService>(notifier);
        _output.WriteLine($"INotifier resolved to {notifier.GetType().Name}");
    }

    [Fact]
    public void AddSignalCli_WithJsonRpcTransport_RegistersJsonRpcClient()
    {
        var configuration = BuildConfiguration(SignalCliTransport.JsonRpc);
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddXUnitLogging(_output);
        services.AddSignalCli(configuration, isDevelopment: false);

        using var sp = services.BuildServiceProvider();
        var notifier = sp.GetRequiredService<INotifier>();
        Assert.IsType<SignalCliJsonRpcClientService>(notifier);
        _output.WriteLine($"INotifier resolved to {notifier.GetType().Name}");
    }

    [Fact]
    public void AddSignalCli_WithJsonRpcTransport_RestClientAlsoResolvable()
    {
        var configuration = BuildConfiguration(SignalCliTransport.JsonRpc);
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddXUnitLogging(_output);
        services.AddSignalCli(configuration, isDevelopment: false);

        using var sp = services.BuildServiceProvider();
        var restClient = sp.GetRequiredService<SignalCliRestClientService>();
        Assert.NotNull(restClient);
        _output.WriteLine($"SignalCliRestClientService is directly resolvable alongside JsonRpc INotifier");
    }

    #endregion

    #region Integration tests (require json-rpc mode server)

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

    #endregion

    #region Private helpers

    private SignalCliJsonRpcClientService CreateJsonRpcService() =>
        new(
            _serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<SignalCliJsonRpcClientService>(),
            _serviceProvider.GetRequiredService<IOptions<SignalCliConfig>>(),
            _serviceProvider.GetRequiredService<SignalCliRestClientService>());

    private static IConfigurationRoot BuildConfiguration(SignalCliTransport transport) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SignalCliConfig.ConfigurationSectionName}:{nameof(SignalCliConfig.TransportMode)}"] = transport.ToString(),
                [$"{SignalCliConfig.ConfigurationSectionName}:{nameof(SignalCliConfig.BaseAddress)}"] = "http://localhost:8080",
                [$"{SignalCliConfig.ConfigurationSectionName}:{nameof(SignalCliConfig.PhoneNumber)}"] = "+441234567890",
            })
            .Build();

    #endregion
}

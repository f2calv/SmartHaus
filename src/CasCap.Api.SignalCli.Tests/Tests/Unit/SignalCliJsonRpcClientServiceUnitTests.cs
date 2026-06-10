using System.Net.WebSockets;

namespace CasCap.Tests.Unit;

/// <summary>
/// Self-contained unit tests for <see cref="SignalCliJsonRpcClientService"/> covering
/// WebSocket URI construction, enum membership, JSON deserialization, and DI registration.
/// </summary>
[Trait("Category", "WebSocket")]
public class SignalCliJsonRpcClientServiceUnitTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("http://localhost:8080", "+49123456789", "ws://localhost:8080/v1/receive/%2B49123456789")]
    [InlineData("https://signal.example.com", "+44780143", "wss://signal.example.com/v1/receive/%2B44780143")]
    [InlineData("http://signalcli.svc.local:8080", "+1234", "ws://signalcli.svc.local:8080/v1/receive/%2B1234")]
    public void BuildWebSocketUri_ConstructsCorrectUri(string baseAddress, string phoneNumber, string expectedUri)
    {
        var uri = SignalCliJsonRpcClientService.BuildWebSocketUri(baseAddress, phoneNumber);
        output.WriteLine($"Input=({baseAddress}, {phoneNumber}) => {uri}");
        Assert.Equal(expectedUri, uri.ToString());
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

        var notification = json.FromJson<SignalCliJsonRpcNotification>();

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

        var notification = json.FromJson<SignalCliJsonRpcNotification>();

        Assert.NotNull(notification);
        Assert.Null(notification.Params);
    }

    [Fact]
    public void AddSignalCli_WithRestTransport_RegistersRestClient()
    {
        var configuration = BuildConfiguration(SignalCliTransport.Normal);
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddXUnitLogging(output);
        services.AddSignalCli(configuration);

        using var sp = services.BuildServiceProvider();
        var notifier = sp.GetRequiredService<INotifier>();
        Assert.IsType<SignalCliRestClientService>(notifier);
        output.WriteLine($"INotifier resolved to {notifier.GetType().Name}");
    }

    [Fact]
    public async Task AddSignalCli_WithJsonRpcTransport_RegistersJsonRpcClient()
    {
        var configuration = BuildConfiguration(SignalCliTransport.JsonRpc);
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddXUnitLogging(output);
        services.AddSignalCli(configuration);

        await using var sp = services.BuildServiceProvider();
        var notifier = sp.GetRequiredService<INotifier>();
        Assert.IsType<SignalCliJsonRpcClientService>(notifier);
        output.WriteLine($"INotifier resolved to {notifier.GetType().Name}");
    }

    [Fact]
    public void AddSignalCli_WithJsonRpcTransport_RestClientAlsoResolvable()
    {
        var configuration = BuildConfiguration(SignalCliTransport.JsonRpc);
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddXUnitLogging(output);
        services.AddSignalCli(configuration);

        using var sp = services.BuildServiceProvider();
        var restClient = sp.GetRequiredService<SignalCliRestClientService>();
        Assert.NotNull(restClient);
        output.WriteLine($"SignalCliRestClientService is directly resolvable alongside JsonRpc INotifier");
    }

    #region Private helpers

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

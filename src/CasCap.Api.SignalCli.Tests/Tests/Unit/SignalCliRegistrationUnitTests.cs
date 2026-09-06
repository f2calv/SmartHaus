namespace CasCap.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ServiceCollectionExtensions.AddSignalCli"/> covering interface
/// registration per transport and basic-auth credential resolution. No signal-cli server is contacted.
/// </summary>
[Trait("Category", "Unit")]
public class SignalCliRegistrationUnitTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(SignalCliTransport.Normal)]
    [InlineData(SignalCliTransport.Native)]
    [InlineData(SignalCliTransport.JsonRpc)]
    [InlineData(SignalCliTransport.JsonRpcNative)]
    public async Task AddSignalCli_RegistersClientInterfaceForEveryTransport(SignalCliTransport transport)
    {
        await using var sp = BuildProvider(transport);

        var client = sp.GetRequiredService<ISignalCliClient>();

        Assert.IsType<SignalCliRestClientService>(client);
        output.WriteLine($"{transport} resolved ISignalCliClient to {client.GetType().Name}");
    }

    [Theory]
    [InlineData(SignalCliTransport.Normal, typeof(SignalCliRestClientService))]
    [InlineData(SignalCliTransport.Native, typeof(SignalCliRestClientService))]
    [InlineData(SignalCliTransport.JsonRpc, typeof(SignalCliJsonRpcClientService))]
    [InlineData(SignalCliTransport.JsonRpcNative, typeof(SignalCliJsonRpcClientService))]
    public async Task AddSignalCli_RegistersReceiverMatchingTheTransport(SignalCliTransport transport, Type expected)
    {
        await using var sp = BuildProvider(transport);

        var receiver = sp.GetRequiredService<ISignalCliReceiver>();

        Assert.IsType(expected, receiver);
    }

    [Fact]
    public async Task AddSignalCli_ResolvesReceiverAndNotifierToTheSameInstance()
    {
        await using var sp = BuildProvider(SignalCliTransport.JsonRpc);

        Assert.Same(sp.GetRequiredService<ISignalCliReceiver>(), sp.GetRequiredService<INotifier>());
    }

    [Fact]
    public void AddSignalCli_IsIdempotent()
    {
        var configuration = BuildConfiguration(SignalCliTransport.Normal);
        var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration).AddXUnitLogging(output);

        services.AddSignalCli(configuration);
        services.AddSignalCli(configuration);

        using var sp = services.BuildServiceProvider();
        Assert.Single(sp.GetServices<ISignalCliClient>());
    }

    [Fact]
    public void BasicAuth_UsesSignalCliCredentialsWhenSupplied()
    {
        using var sp = BuildProvider(SignalCliTransport.Normal, new Dictionary<string, string?>
        {
            [Key(nameof(SignalCliConfig.BasicAuthEnabled))] = "true",
            [Key(nameof(SignalCliConfig.Username))] = "signal-user",
            [Key(nameof(SignalCliConfig.Password))] = "signal-pass",
        });

        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SignalCliConnectionHealthCheck));

        Assert.Equal("Basic", client.DefaultRequestHeaders.Authorization?.Scheme);
        Assert.Equal("signal-user:signal-pass", DecodeBasic(client.DefaultRequestHeaders.Authorization?.Parameter));
    }

    [Fact]
    public void BasicAuth_FallsBackToApiAuthConfig()
    {
        var configuration = BuildConfiguration(SignalCliTransport.Normal, new Dictionary<string, string?>
        {
            [Key(nameof(SignalCliConfig.BasicAuthEnabled))] = "true",
            [$"{ApiAuthConfig.ConfigurationSectionName}:{nameof(ApiAuthConfig.Username)}"] = "shared-user",
            [$"{ApiAuthConfig.ConfigurationSectionName}:{nameof(ApiAuthConfig.Password)}"] = "shared-pass",
        });
        var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration).AddXUnitLogging(output);
        _ = services.AddAndGetCasCapConfiguration<ApiAuthConfig>(configuration);
        services.AddSignalCli(configuration);

        using var sp = services.BuildServiceProvider();
        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SignalCliConnectionHealthCheck));

        Assert.Equal("shared-user:shared-pass", DecodeBasic(client.DefaultRequestHeaders.Authorization?.Parameter));
    }

    [Fact]
    public void BasicAuth_WithoutAnyCredentials_ThrowsWithBothConfigurationKeys()
    {
        using var sp = BuildProvider(SignalCliTransport.Normal, new Dictionary<string, string?>
        {
            [Key(nameof(SignalCliConfig.BasicAuthEnabled))] = "true",
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SignalCliConnectionHealthCheck)));

        Assert.Contains(nameof(SignalCliConfig.Username), ex.Message);
        Assert.Contains(nameof(ApiAuthConfig), ex.Message);
        output.WriteLine(ex.Message);
    }

    #region Private helpers

    private static string Key(string propertyName) => $"{SignalCliConfig.ConfigurationSectionName}:{propertyName}";

    private static string? DecodeBasic(string? parameter) =>
        parameter is null ? null : Encoding.UTF8.GetString(Convert.FromBase64String(parameter));

    private ServiceProvider BuildProvider(SignalCliTransport transport, Dictionary<string, string?>? extra = null)
    {
        var configuration = BuildConfiguration(transport, extra);
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddXUnitLogging(output);
        services.AddSignalCli(configuration);
        return services.BuildServiceProvider();
    }

    private static IConfigurationRoot BuildConfiguration(SignalCliTransport transport, Dictionary<string, string?>? extra = null)
    {
        var settings = new Dictionary<string, string?>
        {
            [Key(nameof(SignalCliConfig.TransportMode))] = transport.ToString(),
            [Key(nameof(SignalCliConfig.BaseAddress))] = "http://localhost:8080",
            [Key(nameof(SignalCliConfig.PhoneNumber))] = "+441234567890",
            [Key(nameof(SignalCliConfig.HealthCheck))] = nameof(KubernetesProbeTypes.None),
        };
        if (extra is not null)
            foreach (var (key, value) in extra)
                settings[key] = value;

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    #endregion
}

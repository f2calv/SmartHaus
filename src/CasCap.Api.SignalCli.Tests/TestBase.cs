namespace CasCap.Tests;

/// <summary>
/// Base class for <see cref="SignalCliRestClientService"/> integration tests.
/// Reads configuration from <c>appsettings.json</c> / <c>appsettings.Development.json</c>
/// and builds a minimal DI container that mirrors the production setup.
/// </summary>
public abstract class TestBase : IAsyncDisposable
{
    /// <summary>The xUnit output helper for writing test diagnostics.</summary>
    protected ITestOutputHelper _output;

    /// <summary>The <see cref="SignalCliRestClientService"/> under test.</summary>
    protected SignalCliRestClientService svc;

    /// <summary>The resolved <see cref="SignalCliConfig"/> from configuration.</summary>
    protected SignalCliConfig _config;

    /// <summary>The group name used for integration tests (from <c>CasCap:CommsAgentConfig:GroupName</c>).</summary>
    protected string _groupName;

    /// <summary>The DI service provider (exposed for sub-service resolution in derived tests).</summary>
    protected readonly ServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestBase"/> class.
    /// </summary>
    protected TestBase(ITestOutputHelper output)
    {
        _output = output;

        var configuration = new ConfigurationBuilder()
            .AddStandardConfiguration(assembly: typeof(TestBase).Assembly)
            .AddKeyVaultConfigurationFrom(c =>
            {
                var authConfig = c.GetSection(AzureAuthConfig.ConfigurationSectionName).Get<AzureAuthConfig>();
                return (authConfig?.KeyVaultUri, authConfig?.TokenCredential);
            })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddXUnitLogging(output);

        _config = services.AddAndGetCasCapConfiguration<SignalCliConfig>(configuration);
        _ = services.AddAndGetCasCapConfiguration<ApiAuthConfig>(configuration);

        _groupName = configuration["CasCap:AIConfig:CommsAgent:Settings:GroupName"]
            ?? throw new GenericException("CasCap:AIConfig:CommsAgent:Settings:GroupName is missing!");

        services.AddHttpClient(nameof(SignalCliConnectionHealthCheck), (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<SignalCliConfig>>().Value;
            client.BaseAddress = new Uri(opts.BaseAddress);
            if (true)
            {
                var authOpts = sp.GetRequiredService<IOptions<ApiAuthConfig>>().Value;
                client.SetBasicAuth(authOpts.Username, authOpts.Password);
            }
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

        services.AddSingleton<SignalCliRestClientService>();

        _serviceProvider = services.BuildServiceProvider();
        svc = _serviceProvider.GetRequiredService<SignalCliRestClientService>();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

using Microsoft.Extensions.Hosting.Internal;

namespace CasCap.Tests;

/// <summary>
/// Base class for <see cref="MieleClientService"/> integration tests.
/// Reads configuration from <c>appsettings.json</c> / <c>appsettings.Development.json</c>
/// and builds a minimal DI container that mirrors the production setup.
/// </summary>
public abstract class TestBase : IAsyncDisposable
{
    /// <summary>The xUnit output helper for writing test diagnostics.</summary>
    protected ITestOutputHelper _output;

    /// <summary>The <see cref="MieleClientService"/> under test.</summary>
    protected MieleClientService svc;

    /// <summary>The resolved <see cref="MieleConfig"/> from configuration.</summary>
    protected MieleConfig _config;

    private readonly ServiceProvider _serviceProvider;

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

        _config = services.AddAndGetCasCapConfiguration<MieleConfig>(configuration);

        //named HttpClient is shared between the client and the healthcheck
        services.AddHttpClient(nameof(MieleConnectionHealthCheck), (s, client) =>
        {
            client.BaseAddress = new Uri(_config.HealthCheckUri);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.OAuthToken);
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

        services.AddSingleton<MieleClientService>();

        IHostEnvironment env = new HostingEnvironment { EnvironmentName = Environments.Development };
        services.AddSingleton(env);

        //assign services to be tested
        _serviceProvider = services.BuildServiceProvider();
        svc = _serviceProvider.GetRequiredService<MieleClientService>();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

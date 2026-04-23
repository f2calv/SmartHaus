using Microsoft.Extensions.Hosting.Internal;

namespace CasCap.Tests;

/// <summary>
/// Base class for Buderus KM200 integration tests.
/// Builds a minimal DI container with <see cref="BuderusKm200ClientService"/> wired up from
/// <c>appsettings.json</c> / <c>appsettings.Development.json</c>.
/// </summary>
public abstract class TestBase : IAsyncDisposable
{
    /// <summary>
    /// xUnit output helper for writing test diagnostic messages.
    /// </summary>
    protected readonly ITestOutputHelper _output;

    /// <summary>
    /// The <see cref="BuderusKm200ClientService"/> under test.
    /// </summary>
    protected readonly BuderusKm200ClientService svc;

    /// <summary>
    /// The resolved <see cref="BuderusConfig"/> from configuration.
    /// </summary>
    protected readonly BuderusConfig _config;

    private readonly ServiceProvider _serviceProvider;

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

        _config = services.AddAndGetCasCapConfiguration<BuderusConfig>(configuration);

        // Named HttpClient shared between client service and health check
        services.AddHttpClient(nameof(BuderusKm200ConnectionHealthCheck), (s, client) =>
        {
            client.BaseAddress = new Uri($"{_config.BaseAddress}:{_config.Port}");
            client.DefaultRequestHeaders.Add("User-Agent", "TeleHeater/2.2.3");
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

        services.AddSingleton<BuderusKm200ClientService>();

        IHostEnvironment env = new HostingEnvironment { EnvironmentName = Environments.Development };
        services.AddSingleton(env);

        _serviceProvider = services.BuildServiceProvider();
        svc = _serviceProvider.GetRequiredService<BuderusKm200ClientService>();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

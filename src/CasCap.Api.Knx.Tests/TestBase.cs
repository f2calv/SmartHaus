namespace CasCap.Tests;

/// <summary>
/// Base class for KNX integration tests providing shared DI container setup.
/// </summary>
public abstract class TestBase : IAsyncDisposable
{
    protected ITestOutputHelper _output;
    protected ServiceProvider _serviceProvider;
    protected KnxGroupAddressLookupService _knxGroupAddressLookupSvc;

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

        //initiate ServiceCollection w/logging
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddXUnitLogging(output);

        var appConfigSection = configuration.GetSection(AppConfig.ConfigurationSectionName);
        var appConfig = appConfigSection.Exists() ? appConfigSection.Get<AppConfig>() : null;
        if (appConfig is not null)
        {
            services.AddCasCapConfiguration<AppConfig>();
            services.AddKnx(configuration);
        }

        //assign services to be tested
        _serviceProvider = services.BuildServiceProvider();
        _knxGroupAddressLookupSvc = _serviceProvider.GetRequiredService<KnxGroupAddressLookupService>();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

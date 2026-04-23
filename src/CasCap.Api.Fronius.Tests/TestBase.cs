using CasCap.HealthChecks;
using Microsoft.Extensions.Hosting.Internal;

namespace CasCap.Tests;

public abstract class TestBase : IAsyncDisposable
{
    protected ITestOutputHelper _output;
    protected FroniusClientService svc;

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

        //initiate ServiceCollection w/logging
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddXUnitLogging(output);

        var config = configuration.GetCasCapConfiguration<FroniusConfig>();

        //add services
        //services.AddFronius(configuration);

        //named HttpClient is shared between the client and the healthcheck
        services.AddHttpClient(nameof(FroniusSymoConnectionHealthCheck), (s, client) =>
        {
            client.BaseAddress = new Uri(config.BaseAddress);
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
        //.AddPolicyHandler((provider, _) => GetStandardRetryPolicy(provider))
        ;
        services.AddSingleton<FroniusClientService>();

        IHostEnvironment env = new HostingEnvironment { EnvironmentName = Environments.Development };
        services.AddSingleton(env);

        //assign services to be tested
        _serviceProvider = services.BuildServiceProvider();
        svc = _serviceProvider.GetRequiredService<FroniusClientService>();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

namespace CasCap.Tests;

public abstract class TestBase : IAsyncDisposable
{
    protected ITestOutputHelper _output;
    protected DoorBirdClientService svc;
    protected DoorBirdConfig _config;

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

        _config = configuration.GetCasCapConfiguration<DoorBirdConfig>();

        //add services
        //services.AddDoorBird(configuration, appConfig);

        //named HttpClient is shared between the client and the healthcheck
        services.AddHttpClient(nameof(DoorBirdConnectionHealthCheck), (s, client) =>
        {
            client.BaseAddress = new Uri(_config.BaseAddress);
            client.SetBasicAuth(_config.Username, _config.Password);
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
        //.AddPolicyHandler((provider, _) => GetStandardRetryPolicy(provider))
        ;
        services.AddSingleton<DoorBirdClientService>();

        IHostEnvironment env = new HostingEnvironment { EnvironmentName = Environments.Development };
        services.AddSingleton(env);

        //assign services to be tested
        _serviceProvider = services.BuildServiceProvider();
        svc = _serviceProvider.GetRequiredService<DoorBirdClientService>();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

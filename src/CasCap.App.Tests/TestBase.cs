namespace CasCap.Tests;

public abstract class TestBase
{
    protected ITestOutputHelper _output;
    protected AIConfig _aiConfig;
    protected IConfiguration _configuration;

    protected TestBase(ITestOutputHelper output)
    {
        _output = output;

        var configuration = new ConfigurationBuilder()
            .AddStandardConfiguration(assembly: typeof(TestBase).Assembly)
            //Loaded last so it wins over the deployment-oriented Local files, whose endpoints are
            //  cluster-internal and unreachable from a workstation. Gitignored; see the test README.
            .AddJsonFile("appsettings.Tests.Local.json", optional: true, reloadOnChange: true)
            .AddKeyVaultConfigurationFrom(c =>
            {
                var authConfig = c.GetSection(AzureAuthConfig.ConfigurationSectionName).Get<AzureAuthConfig>();
                return (authConfig?.KeyVaultUri, authConfig?.TokenCredential);
            })
            .Build();

        _configuration = configuration;

        //initiate ServiceCollection w/logging
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddXUnitLogging(output);

        _aiConfig = services.AddAndGetCasCapConfiguration<AIConfig>(configuration);
    }
}

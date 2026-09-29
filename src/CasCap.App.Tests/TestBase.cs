namespace CasCap.Tests;

public abstract class TestBase
{
    protected ITestOutputHelper _output;
    protected AIConfig _aiConfig;
    protected IConfiguration _configuration;
    protected AzureAuthConfig? _azureAuthConfig;

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

        _configuration = configuration;
        _azureAuthConfig = configuration.GetSection(AzureAuthConfig.ConfigurationSectionName).Get<AzureAuthConfig>();

        //initiate ServiceCollection w/logging
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddXUnitLogging(output);

        _aiConfig = services.AddAndGetCasCapConfiguration<AIConfig>(configuration);
    }
}

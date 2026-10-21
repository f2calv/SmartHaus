using CasCap.Tests.Infrastructure;

namespace CasCap.Tests.Integration;

/// <summary>Covers host startup configuration binding and safe core service registration.</summary>
[Trait("Category", "Integration")]
public class HostStartupConfigurationTests : WebApiTestBase
{
    [Fact]
    public void Startup_BindsConfigurationAndResolvesCoreServices()
    {
        var appConfig = Services.GetRequiredService<IOptions<AppConfig>>().Value;
        var featureConfig = Services.GetRequiredService<IOptions<FeatureConfig>>().Value;
        var apiAuthConfig = Services.GetRequiredService<IOptions<ApiAuthConfig>>().Value;
        var featureFlagConfig = Services.GetRequiredService<IOptions<FeatureFlagConfig>>().Value;

        Assert.Equal(CasCapAppWebApplicationFactory.TestHausName, appConfig.HausName);
        Assert.False(appConfig.IsKeyVaultEnabled);
        Assert.Equal(FeatureNames.Test, featureConfig.EnabledFeatures);
        Assert.Equal(CasCapAppWebApplicationFactory.TestUsername, apiAuthConfig.Username);
        Assert.Equal(CasCapAppWebApplicationFactory.TestPassword, apiAuthConfig.Password);
        Assert.Single(featureFlagConfig.EnabledFeatures, FeatureNames.Test);
        Assert.Same(TimeProvider.System, Services.GetRequiredService<TimeProvider>());
        Assert.NotNull(Services.GetRequiredService<ILocalCache>());
        Assert.NotNull(Services.GetRequiredService<ApplicationMetadata>());
    }
}

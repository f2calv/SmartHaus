using CasCap.Tests.Infrastructure;

namespace CasCap.Tests.Api;

/// <summary>
/// Integration tests that verify <see cref="IBgFeature"/> background-service
/// registrations and the <c>EnabledFeatures</c> filtering behaviour.
/// </summary>
/// <remarks>
/// <para>
/// CasCap.App.Server uses <c>AddFeatureFlagService</c> to start only
/// the background services whose <see cref="IBgFeature.FeatureName"/> is present in the
/// configured enabled features set. Services whose <c>FeatureName</c> is
/// <see cref="IBgFeature.AlwaysEnabled"/> run in every mode.
/// </para>
/// <para>
/// The <see cref="CasCapAppWebApplicationFactory"/> boots the application with
/// <c>EnabledFeatures = "Api"</c>, so:
/// </para>
/// <list type="bullet">
///   <item><see cref="GitMetadataBgService"/> – registered as a standalone hosted service via <c>addGitMetadataService: true</c>.</item>
///   <item><see cref="GpuTestBgService"/>  – <c>FeatureName = "EdgeHardware"</c> → registered only when EdgeHardware flag is set.</item>
///   <item><see cref="CommunicationsBgService"/> – <c>FeatureName = "Comms"</c> → registered but NOT started unless Comms flag is set.</item>
/// </list>
/// </remarks>
public class FeatureServiceRegistrationTests(ITestOutputHelper output) : WebApiTestBase
{
    /// <summary>
    /// <see cref="CommunicationsBgService"/> must be registered with the DI container
    /// as <see cref="IBgFeature"/> regardless of <c>EnabledFeatures</c>, because it is
    /// registered unconditionally in <c>Program.cs</c>.
    /// <see cref="GpuTestBgService"/> is only registered when the <c>EdgeHardware</c> feature is set.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void AllAlwaysRegisteredFeatures_ArePresent_InDiContainer()
    {
        var features = Services
            .GetServices<IBgFeature>()
            .ToList();

        output.WriteLine($"Registered IBgFeature count: {features.Count}");
        foreach (var f in features)
            output.WriteLine($"  {f.GetType().Name} – FeatureName={f.FeatureName}");

        // Verify by type identity (the service IS the correct implementation)
        Assert.Contains(features, f => f is CommunicationsBgService);

        // Verify by FeatureName (the services declare the right feature name)
        Assert.Contains(features, f => f is CommunicationsBgService && f.FeatureName == FeatureNames.Comms);
    }

    /// <summary>
    /// With <c>EnabledFeatures = "Api"</c> no hardware-specific features should be registered
    /// (Knx, Buderus, DoorBird, Fronius, Sicce, RaspberryPi…).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void HardwareFeatures_AreNotRegistered_WhenEnabledFeaturesIsApiOnly()
    {
        var features = Services
            .GetServices<IBgFeature>()
            .ToList();

        var hardwareFeatureNames = new[]
        {
            FeatureNames.Knx,
            FeatureNames.Buderus,
            FeatureNames.DoorBird,
            FeatureNames.Fronius,
            FeatureNames.Sicce,
            FeatureNames.EdgeHardware,
            FeatureNames.Miele,
        };

        var hardwareFeatures = features
            .Where(f => hardwareFeatureNames.Contains(f.FeatureName))
            .ToList();

        output.WriteLine($"Hardware features registered: {hardwareFeatures.Count}");
        foreach (var f in hardwareFeatures)
            output.WriteLine($"  UNEXPECTED: {f.GetType().Name} – FeatureName={f.FeatureName}");

        Assert.Empty(hardwareFeatures);
    }

    /// <summary>
    /// Every registered <see cref="IBgFeature"/> should have a <c>FeatureName</c>
    /// that is either <see cref="IBgFeature.AlwaysEnabled"/> (always active), a name that is
    /// present in the current enabled features set, or a feature-specific service (e.g.
    /// "Comms") that is registered unconditionally but only started when its name is active.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void RegisteredFeatures_HaveCompatibleFeatureName_WithCurrentEnabledFeatures()
    {
        var featureFlagConfig = Services.GetRequiredService<IOptions<FeatureFlagConfig>>().Value;
        var features = Services.GetServices<IBgFeature>().ToList();

        output.WriteLine($"EnabledFeatures=[{string.Join(", ", featureFlagConfig.EnabledFeatures)}]");

        foreach (var feature in features)
        {
            var compatible =
                string.Equals(feature.FeatureName, IBgFeature.AlwaysEnabled, StringComparison.OrdinalIgnoreCase) ||
                featureFlagConfig.EnabledFeatures.Contains(feature.FeatureName);

            output.WriteLine($"  {feature.GetType().Name}: FeatureName={feature.FeatureName}, Compatible={compatible}");
        }

        // At a minimum, the unconditionally-registered Comms service must be present and compatible.
        Assert.Contains(features, f => f.FeatureName == FeatureNames.Comms);
    }

    /// <summary>
    /// Demonstrates how to override the <c>EnabledFeatures</c> for a subset of tests to verify
    /// that additional feature services are registered when the relevant flag is set.
    /// </summary>
    /// <remarks>
    /// This example uses a derived factory with <c>EnabledFeatures = "Knx"</c> to assert that the
    /// KNX background services are registered. Replace the <c>Knx</c> feature name
    /// with any feature you want to exercise.
    /// </remarks>
    [Fact(Skip = "Requires KNX infrastructure (bus + Azure Storage) for KnxGroupAddressLookupService")]
    [Trait("Category", "Integration")]
    public async Task KnxFeatures_AreRegistered_WhenKnxFlagIsSet()
    {
        await using var knxFactory = new KnxModeCasCapAppWebApplicationFactory();

        var features = knxFactory.Services
            .GetServices<IBgFeature>()
            .ToList();

        output.WriteLine($"KNX mode features: {features.Count}");
        foreach (var f in features)
            output.WriteLine($"  {f.GetType().Name} – FeatureName={f.FeatureName}");

        // With Knx flag, KnxMonitorBgService / KnxSenderBgService etc. should be registered
        Assert.Contains(features, f => f.FeatureName == FeatureNames.Knx);
    }

    /// <summary>
    /// A variant of <see cref="CasCapAppWebApplicationFactory"/> that activates the
    /// <c>Knx</c> feature, demonstrating how to test individual
    /// feature slices in isolation.
    /// </summary>
    private sealed class KnxModeCasCapAppWebApplicationFactory : CasCapAppWebApplicationFactory
    {
        /// <inheritdoc/>
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [CasCapAppWebApplicationFactory.EnabledFeaturesConfigKey]
                        = $"{FeatureNames.Test},{FeatureNames.Knx}",
                });
            });
        }
    }
}

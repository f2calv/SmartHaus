namespace CasCap.Tests.Unit;

/// <summary>Covers direct parsing and validation of enabled feature names.</summary>
[Trait("Category", "Configuration")]
public class FeatureConfigTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" , ")]
    public void GetEnabledFeatures_EmptyValuesReturnEmptySet(string value)
    {
        var config = new FeatureConfig { EnabledFeatures = value };

        Assert.Empty(config.GetEnabledFeatures());
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Test,Unknown")]
    public void GetEnabledFeatures_UnknownNamesAreRejected(string value)
    {
        var config = new FeatureConfig { EnabledFeatures = value };

        var exception = Assert.Throws<InvalidOperationException>(config.GetEnabledFeatures);

        Assert.Contains("Unknown", exception.Message);
    }

    [Fact]
    public void GetEnabledFeatures_KnownNamesAreParsedCaseInsensitively()
    {
        var config = new FeatureConfig { EnabledFeatures = " test , KNX " };

        var enabledFeatures = config.GetEnabledFeatures();

        Assert.Equal(2, enabledFeatures.Count);
        Assert.Contains(FeatureNames.Test, enabledFeatures);
        Assert.Contains(FeatureNames.Knx, enabledFeatures);
    }
}

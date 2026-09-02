namespace CasCap.Tests;

/// <summary>
/// Deserialization tests for firmware-dependent unit value shapes.
/// </summary>
public sealed class UnitValueTests
{
    [Theory]
    [InlineData("{\"Unit\":\"W\",\"Value\":466.96435546875}", 466.96435546875)]
    [InlineData("{\"Unit\":\"W\",\"Values\":{\"1\":466.96435546875}}", 466.96435546875)]
    [Trait("Category", "Deserialization")]
    public void DeserializesLegacyAndCurrentFirmwareShapes(string json, double expected)
    {
        var result = json.FromJson<UnitValue>();
        var value = result?.Value ?? result?.Values?.Values.SingleOrDefault();

        Assert.Equal(expected, value);
    }
}
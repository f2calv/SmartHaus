namespace CasCap.Tests.Unit;

/// <summary>
/// Checks that the MCP prompts shipped with SmartHaus only name tools that exist, since a prompt naming a
/// removed or renamed tool steers the model towards a call it cannot make.
/// </summary>
[Trait("Category", "McpPrompts")]
public sealed class McpPromptToolReferenceTests
{
    [Fact]
    public void McpPrompts_ReferenceExistingTools()
    {
        var missing = McpPromptContract.FindMissingToolReferences([SmartHausEvaluationTestData.HausAssembly]);

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }
}

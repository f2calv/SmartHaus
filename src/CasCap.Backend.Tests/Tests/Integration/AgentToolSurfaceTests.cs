namespace CasCap.Tests.Integration;

/// <summary>
/// Measures and checks the tool surface every configured agent sends to its model, without calling a model.
/// </summary>
/// <remarks>
/// Every request repeats the full tool surface, so its size is a fixed prompt cost per request — and per
/// sub-agent hop when an orchestrator delegates. Integration only because it reads the agent configuration.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Category", "ToolSurface")]
public sealed class AgentToolSurfaceTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task ToolSurface_DescribedAndMeasured()
    {
        await using var logging = new ServiceCollection()
            .AddXUnitLogging(_output)
            .AddLogging(b => b.SetMinimumLevel(LogLevel.Warning))
            .BuildServiceProvider();
        var harness = SmartHausEvaluationTestData.CreateHarness(logging.GetRequiredService<ILoggerFactory>(), _aiConfig);

        var violations = new List<string>();
        _output.WriteLine("| Agent | Tools | Side-effect tools | Tool schema chars (~tokens) | Instruction chars (~tokens) |");
        _output.WriteLine("| --- | --- | --- | --- | --- |");
        foreach (var (agentKey, agentConfig) in _aiConfig.Agents.Where(a => a.Value.Enabled).OrderBy(a => a.Key, StringComparer.Ordinal))
        {
            var tools = harness.GetOfferedTools(agentKey);
            var schemaCharacters = ToolFootprint.Characters(tools);
            var instructions = AgentExtensions.ResolveInstructions(agentConfig, SmartHausEvaluationTestData.HausAssembly, _aiConfig);
            _output.WriteLine($"| {agentKey} | {tools.Count} | {tools.Count(t => t.Disposition is ToolDisposition.SideEffect)} "
                + $"| {schemaCharacters} (~{ToolFootprint.ApproximateTokens(schemaCharacters)}) "
                + $"| {instructions.Length} (~{ToolFootprint.ApproximateTokens(instructions.Length)}) |");

            violations.AddRange(tools.GroupBy(t => t.Name).Where(g => g.Count() > 1)
                .Select(g => $"{agentKey}: tool {g.Key} is offered {g.Count()} times"));
            violations.AddRange(tools.Where(t => string.IsNullOrWhiteSpace(t.Description))
                .Select(t => $"{agentKey}: tool {t.Name} has no description"));
            violations.AddRange(tools.SelectMany(t => GetUndescribedParameters(t).Select(p => $"{agentKey}: {t.Name}.{p} has no description")));
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations.Distinct()));
    }

    private static IEnumerable<string> GetUndescribedParameters(AIFunction tool) =>
        tool.JsonSchema.TryGetProperty("properties", out var properties) && properties.ValueKind is JsonValueKind.Object
            ? properties.EnumerateObject()
                .Where(p => !p.Value.TryGetProperty("description", out var d) || string.IsNullOrWhiteSpace(d.GetString()))
                .Select(p => p.Name)
            : [];
}

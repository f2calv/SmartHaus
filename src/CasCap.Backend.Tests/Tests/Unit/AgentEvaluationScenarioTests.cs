namespace CasCap.Tests.Unit;

/// <summary>
/// Tests for the SmartHaus evaluation scenarios and synthetic house. The evaluation framework itself is
/// tested in <c>CasCap.Common.AI.Tests</c>.
/// </summary>
[Trait("Category", "AgentEvaluation")]
public sealed class AgentEvaluationScenarioTests
{
    private static readonly string[] AgentKeyNames =
    [
        AgentKeys.SecurityAgent, AgentKeys.HeatingAgent, AgentKeys.CommsAgent, AgentKeys.EnergyAgent,
        AgentKeys.HomeControlAgent, AgentKeys.InfraAgent, AgentKeys.AppliancesAgent,
    ];

    [Theory]
    [InlineData("The front door is currently **unlocked**.", true)]
    [InlineData("The front door is not locked.", true)]
    [InlineData("The front door is locked; the garage side door is unlocked.", false)]
    [InlineData("The front door is closed and 1 of 2 doors is unlocked.", false)]
    public void FrontDoorLockedAnswer_AnchorsStateToFrontDoor(string answer, bool satisfied) =>
        Assert.Equal(satisfied, AgentEvaluationScenarioTestData.Get("front-door-locked").Answer.IsSatisfiedBy(answer));

    [Fact]
    public void Grade_FailsMissingToolAndSideEffect()
    {
        var scenario = AgentEvaluationScenarioTestData.Get("lights-on-count");
        RecordedToolCall[] toolCalls =
        [
            new(AgentKeys.HomeControlAgent, "get_house_contact_states", ToolDisposition.Query, "{}", FixtureFound: false),
            new(AgentKeys.HomeControlAgent, "create_poll", ToolDisposition.SideEffect, "{}", FixtureFound: true),
        ];

        var (answerPassed, toolSelectionPassed, reasons) = AgentEvaluationGrader.Grade(scenario, "5 lights are on.", toolCalls);

        Assert.True(answerPassed);
        Assert.False(toolSelectionPassed);
        Assert.Contains("did not call get_house_light_states or get_house_smart_lights", reasons);
        Assert.Contains("attempted side effect create_poll", reasons);
    }

    [Theory]
    [InlineData("get_house_light_states", ToolDisposition.Query)]
    [InlineData("switch_off_all_house_lights", ToolDisposition.SideEffect)]
    [InlineData("create_poll", ToolDisposition.SideEffect)]
    [InlineData("unlock_house_door", ToolDisposition.SideEffect)]
    public void Catalog_ClassifiesSmartHausTools(string toolName, ToolDisposition expected) =>
        Assert.Equal(expected, SmartHausEvaluationTestData.Catalog.Classify(GetTool(toolName)));

    [Fact]
    public async Task FixtureToolFunction_ReturnsFilteredLightingFixture()
    {
        var recorder = new EvaluationRecorder();
        var inner = GetTool("get_house_light_states");
        var tool = new FixtureToolFunction(inner, AgentKeys.HomeControlAgent, ToolDisposition.Query,
            SyntheticHouseTestData.LightingResponses["get_house_light_states"], recorder);

        var result = await tool.InvokeAsync(new AIFunctionArguments { ["floor"] = "DG" }, TestContext.Current.CancellationToken);

        var states = Assert.IsType<JsonElement>(result);
        Assert.Equal(SyntheticHouseTestData.Lights.Count(l => l.Floor == FloorType.DG), states.EnumerateObject().Count());
        Assert.True(Assert.Single(recorder.ToolCalls).FixtureFound);
    }

    [Fact]
    public void Scenarios_ReferenceKnownTools()
    {
        var scenarios = AgentEvaluationScenarioTestData.Scenarios;
        var known = SmartHausEvaluationTestData.Catalog.GetAllTools().Select(tool => tool.Name)
            .Append("get_current_datetime_state")
            .Concat(AgentKeyNames.Select(agentName => $"invoke_{agentName.ToSnakeCase()}"))
            .Concat(scenarios.SelectMany(scenario => scenario.ToolSurfaceVariants)
                .SelectMany(variant => variant.CandidateTools.Values.SelectMany(tools => tools))
                .Select(tool => tool.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unknown = scenarios
            .SelectMany(s => s.ToolResponses.Keys
                .Concat(s.RequiredToolGroups.SelectMany(g => g))
                .Concat(s.ForbiddenTools)
                .Concat(s.ToolSurfaceVariants.SelectMany(v => v.DescriptionOverrides.Keys
                    .Concat(v.ParameterDescriptionOverrides.Keys.Select(k => k[..k.IndexOf('.', StringComparison.Ordinal)]))
                    .Concat(v.HiddenTools)
                    .Concat(v.ToolAllowLists.Values.SelectMany(a => a))))
                .Where(name => !known.Contains(name))
                .Select(name => $"{s.Id}: {name}"))
            .Distinct()
            .ToList();

        Assert.True(unknown.Count == 0, string.Join(Environment.NewLine, unknown));
        Assert.Equal(scenarios.Count, scenarios.Select(s => s.Id).Distinct().Count());
    }

    private static AIFunction GetTool(string toolName) =>
        SmartHausEvaluationTestData.Catalog.GetAllTools().Single(t => t.Name == toolName);
}

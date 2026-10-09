namespace CasCap.Tests;

/// <summary>
/// The agent evaluation scenarios and the instruction and tool surface variants they compare.
/// </summary>
/// <remarks>
/// Add a scenario by choosing the agent that should answer, the question, the synthetic responses from
/// <see cref="SyntheticHouseTestData"/> and the checks. Add a variant to test a wording or tool change
/// against the baseline before shipping it.
/// </remarks>
public static class AgentEvaluationScenarioTestData
{
    /// <summary>Asks every agent to answer state questions directly rather than through a poll.</summary>
    public static InstructionVariant AnswerDirectly { get; } = new()
    {
        Name = "answer-directly",
        Notes = "Adds a rule that state questions get a direct answer; tests whether the poll rules pull small models towards create_poll.",
        Rewrite = (_, baseline) => baseline + """


            ## Answering questions

            When the user asks about the current state of the house, call the tool that reads that state and answer
            in one short sentence with the value. Do not create a poll unless the user asks to choose between options.
            """,
    };

    /// <summary>States where the outdoor temperature comes from.</summary>
    public static InstructionVariant HouseFacts { get; } = new()
    {
        Name = "house-facts",
        Notes = "Adds the fact that the only outdoor sensor is the heat pump's, on the north wall.",
        Rewrite = (_, baseline) => baseline + """


            ## House facts

            - The only outdoor temperature sensor is the heat pump's outdoor sensor, mounted on the north wall.
              Use it for any question about the temperature outside, including the north side of the house.
            """,
    };

    /// <summary>Offers the home control agent only its lighting and structure tools.</summary>
    public static ToolSurfaceVariant LightingOnly { get; } = new()
    {
        Name = "lighting-only",
        Notes = "Tests whether a smaller tool surface improves a small language model's tool choice and latency.",
        ToolAllowLists = new Dictionary<string, string[]>
        {
            [AgentKeys.HomeControlAgent] =
            [
                "get_current_datetime_state",
                "get_house_floors",
                "get_house_rooms",
                "get_house_light_states",
                "get_house_smart_lights",
                "get_house_light_state",
            ],
        },
    };

    /// <summary>Describes the heat pump state tool as the source of the north wall outdoor reading.</summary>
    public static ToolSurfaceVariant OutdoorSensorDescribed { get; } = new()
    {
        Name = "outdoor-sensor-described",
        Notes = "The shipped description says nothing about outdoor temperature or where the sensor is.",
        DescriptionOverrides = new Dictionary<string, string>
        {
            ["get_heat_pump_state"] = "Current heat pump state, including the outdoor temperature measured on the north wall of the house, heating circuit setpoints, hot water temperatures and operating mode.",
        },
    };

    /// <summary>Proposes a dedicated outdoor temperature tool for the heating agent.</summary>
    public static ToolSurfaceVariant OutdoorTemperatureCandidate { get; } = new()
    {
        Name = "outdoor-temperature-tool",
        Notes = "Evaluates a proposed get_house_outdoor_temperature tool before implementing it.",
        CandidateTools = new Dictionary<string, AIFunction[]>
        {
            [AgentKeys.HeatingAgent] =
            [
                AIFunctionFactory.Create(
                    () => (object?)null,
                    "get_house_outdoor_temperature",
                    "Current outdoor air temperature in °C and the side of the house where the sensor is mounted."),
            ],
        },
    };

    /// <summary>Every scenario, in execution order.</summary>
    public static IReadOnlyList<AgentEvaluationScenario> Scenarios { get; } =
    [
        new()
        {
            Id = "lights-on-count",
            AgentKey = AgentKeys.HomeControlAgent,
            Question = "How many lights are turned on?",
            Answer = AnswerExpectation.Number(SyntheticHouseTestData.LightsOn),
            RequiredToolGroups = [["get_house_light_states", "get_house_smart_lights"]],
            ToolResponses = SyntheticHouseTestData.Merge(SyntheticHouseTestData.CommonResponses, SyntheticHouseTestData.LightingResponses),
            InstructionVariants = [InstructionVariant.Baseline, AnswerDirectly],
            ToolSurfaceVariants = [ToolSurfaceVariant.Baseline, LightingOnly],
            Notes = "Counting over a state dictionary with one unknown light. The agent is offered roughly 37 tools.",
        },
        new()
        {
            Id = "lights-on-count-via-comms",
            AgentKey = AgentKeys.CommsAgent,
            Question = "How many lights are turned on?",
            Answer = AnswerExpectation.Number(SyntheticHouseTestData.LightsOn),
            RequiredToolGroups =
            [
                [$"invoke_{AgentKeys.HomeControlAgent.ToSnakeCase()}"],
                ["get_house_light_states", "get_house_smart_lights"],
            ],
            ToolResponses = SyntheticHouseTestData.Merge(SyntheticHouseTestData.CommonResponses, SyntheticHouseTestData.LightingResponses),
            InstructionVariants = [InstructionVariant.Baseline, AnswerDirectly],
            ToolSurfaceVariants = [ToolSurfaceVariant.Baseline, LightingOnly],
            Notes = "The production path: the orchestrator delegates to the home control agent. Compare the request timeline with lights-on-count to see the delegation cost.",
        },
        new()
        {
            Id = "outdoor-temperature-north",
            AgentKey = AgentKeys.HeatingAgent,
            Question = "What is the temperature outside on the north side of our house?",
            Answer = AnswerExpectation.Number(SyntheticHouseTestData.OutdoorTemperature, tolerance: 0.05),
            RequiredToolGroups = [["get_heat_pump_state", "get_house_outdoor_temperature"]],
            ToolResponses = SyntheticHouseTestData.Merge(SyntheticHouseTestData.CommonResponses, SyntheticHouseTestData.HeatingResponses),
            InstructionVariants = [InstructionVariant.Baseline, HouseFacts],
            ToolSurfaceVariants = [ToolSurfaceVariant.Baseline, OutdoorSensorDescribed, OutdoorTemperatureCandidate],
            Notes = "No shipped tool mentions an outdoor or north-side temperature; the answer is buried in the heat pump snapshot.",
        },
        new()
        {
            Id = "front-door-locked",
            AgentKey = AgentKeys.HomeControlAgent,
            Question = "Is the front door locked?",
            Answer = AnswerExpectation.Matches(
                @"front door(?:'s)?(?: lock)? (?:is|remains)(?: currently| still)? \**(?:unlocked|not locked)\b",
                "says the front door is unlocked"),
            RequiredToolGroups = [["get_house_door_lock_states"]],
            ToolResponses = SyntheticHouseTestData.Merge(SyntheticHouseTestData.CommonResponses, SyntheticHouseTestData.DoorResponses),
            InstructionVariants = [InstructionVariant.Baseline, AnswerDirectly],
            Notes = "The door is closed but unlocked; a model that reads the contact sensor instead of the lock answers wrongly.",
        },
    ];

    /// <summary>Scenario identifiers for theory data.</summary>
    public static TheoryData<string> ScenarioIds => [.. Scenarios.Select(s => s.Id)];

    /// <summary>Returns a scenario by identifier.</summary>
    /// <param name="id">The scenario identifier.</param>
    public static AgentEvaluationScenario Get(string id) => Scenarios.Single(s => s.Id == id);
}

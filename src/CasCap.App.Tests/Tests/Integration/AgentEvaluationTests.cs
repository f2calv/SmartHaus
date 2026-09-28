using System.ComponentModel.DataAnnotations;

namespace CasCap.Tests.Integration;

/// <summary>
/// End-to-end evaluation of SmartHaus agents against live models, with synthetic tool responses.
/// </summary>
/// <remarks>
/// Each scenario runs against every configured model, instruction variant and tool surface variant,
/// <see cref="AgentEvaluationConfig.Repetitions"/> times. Results are written to the test output and to
/// <see cref="AgentEvaluationConfig.ResultsDirectory"/>; only the baseline pass rate is asserted.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Category", "AgentEvaluation")]
public sealed class AgentEvaluationTests(ITestOutputHelper output) : TestBase(output)
{
    [Theory]
    [MemberData(nameof(AgentEvaluationScenarioTestData.ScenarioIds), MemberType = typeof(AgentEvaluationScenarioTestData))]
    public async Task EvaluateScenario(string scenarioId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var scenario = AgentEvaluationScenarioTestData.Get(scenarioId);
        var config = _configuration.GetSection(AgentEvaluationConfig.ConfigurationSectionName).Get<AgentEvaluationConfig>()
            ?? new AgentEvaluationConfig();
        Validator.ValidateObject(config, new ValidationContext(config), validateAllProperties: true);

        await using var logging = new ServiceCollection()
            .AddXUnitLogging(_output)
            .AddLogging(b => b.SetMinimumLevel(LogLevel.Warning))
            .BuildServiceProvider();
        using var throttlingMonitor = new AzureThrottlingMonitor();
        var harness = SmartHausEvaluationTestData.CreateHarness(logging.GetRequiredService<ILoggerFactory>(), _aiConfig,
            _azureAuthConfig?.TokenCredential, config, throttlingMonitor);
        var configuredProviderKeys = SmartHausEvaluationTestData.GetProviderKeys(config);

        var providerKeys = GetEvaluableProviders(harness, configuredProviderKeys);
        if (providerKeys.Count == 0)
            Assert.Skip($"No evaluable provider among {string.Join(", ", configuredProviderKeys)}; configure {AgentEvaluationConfig.ConfigurationSectionName}.");

        var instructionVariants = Select(scenario.InstructionVariants, config.InstructionVariants, v => v.Name);
        var toolSurfaceVariants = Select(scenario.ToolSurfaceVariants, config.ToolSurfaceVariants, v => v.Name);

        var runs = new List<AgentEvaluationRun>();
        var warmUpFailures = new List<string>();
        foreach (var providerKey in providerKeys)
        {
            if (await TryWarmUpAsync(harness, providerKey, config, cancellationToken) is { } failure)
            {
                warmUpFailures.Add(failure);
                continue;
            }

            runs.AddRange(await RunVariantsAsync(harness, scenario, providerKey, instructionVariants, toolSurfaceVariants,
                config, cancellationToken));
        }

        var summaries = AgentEvaluationReport.Summarise(runs);
        await WriteReportAsync(scenario, runs, summaries, config, configuredProviderKeys, cancellationToken);

        var belowThreshold = summaries
            .Where(s => s.InstructionVariant == instructionVariants[0].Name && s.ToolSurfaceVariant == toolSurfaceVariants[0].Name)
            .Where(s => s.PassRate < config.MinimumPassRate)
            .Select(s => $"{s.ProviderKey} ({s.ModelName}) passed {s.Passed}/{s.Runs}: {string.Join("; ", s.TopFailureReasons)}")
            .Concat(warmUpFailures)
            .ToList();
        Assert.True(belowThreshold.Count == 0,
            $"Baseline pass rate below {config.MinimumPassRate:P0} for {scenario.Id}: {string.Join(" | ", belowThreshold)}");
    }

    private List<string> GetEvaluableProviders(AgentEvaluationHarness harness, IReadOnlyList<string> configuredProviderKeys)
    {
        var providerKeys = new List<string>();
        foreach (var providerKey in configuredProviderKeys)
        {
            if (harness.GetUnavailableReason(providerKey) is { } reason)
                _output.WriteLine($"Skipping provider {providerKey}: {reason}.");
            else
                providerKeys.Add(providerKey);
        }
        return providerKeys;
    }

    /// <summary>Warms the provider up and records its plain chat latency.</summary>
    /// <returns>The failure to report, or <see langword="null"/> when the provider is ready.</returns>
    private async Task<string?> TryWarmUpAsync(AgentEvaluationHarness harness, string providerKey, AgentEvaluationConfig config,
        CancellationToken cancellationToken)
    {
        try
        {
            var warmUpTimeout = TimeSpan.FromSeconds(config.WarmUpTimeoutSeconds);
            var warmUp = await harness.WarmUpAsync(providerKey, warmUpTimeout, cancellationToken);
            var plainChat = await harness.WarmUpAsync(providerKey, warmUpTimeout, cancellationToken);
            await AgentEvaluationReport.AppendPlainChatAsync(config.ResultsDirectory, providerKey, plainChat, cancellationToken);
            _output.WriteLine($"{providerKey} warm-up took {warmUp.TotalSeconds:0.0}s; plain chat {plainChat.TotalSeconds:0.0}s.");
            return null;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return $"{providerKey} warm-up failed: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private async Task<List<AgentEvaluationRun>> RunVariantsAsync(AgentEvaluationHarness harness, AgentEvaluationScenario scenario,
        string providerKey, List<InstructionVariant> instructionVariants, List<ToolSurfaceVariant> toolSurfaceVariants,
        AgentEvaluationConfig config, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(config.RunTimeoutSeconds);
        var runs = new List<AgentEvaluationRun>();
        foreach (var instructionVariant in instructionVariants)
            foreach (var toolSurfaceVariant in toolSurfaceVariants)
                for (var repetition = 1; repetition <= config.Repetitions; repetition++)
                {
                    var run = await harness.RunAsync(scenario, providerKey, instructionVariant, toolSurfaceVariant,
                        repetition, timeout, cancellationToken);
                    _output.WriteLine($"{providerKey} / {instructionVariant} / {toolSurfaceVariant} #{repetition}: "
                        + $"{(run.Passed ? "pass" : "FAIL")} in {run.Elapsed.TotalSeconds:0.0}s — {Truncate(run.Answer)}"
                        + (run.FailureReasons.Count > 0 ? $" [{string.Join("; ", run.FailureReasons)}]" : string.Empty));
                    runs.Add(run);
                }
        return runs;
    }

    private async Task WriteReportAsync(AgentEvaluationScenario scenario, List<AgentEvaluationRun> runs,
        IReadOnlyList<AgentEvaluationSummary> summaries, AgentEvaluationConfig config, IReadOnlyList<string> configuredProviderKeys,
        CancellationToken cancellationToken)
    {
        _output.WriteLine(string.Empty);
        _output.WriteLine(AgentEvaluationReport.ToMarkdown(summaries));
        foreach (var run in runs.Where(r => r.Repetition == 1))
            _output.WriteLine(AgentEvaluationReport.ToTimelineMarkdown(run));

        var directory = await AgentEvaluationReport.WriteAsync(config.ResultsDirectory, scenario.Id, runs, cancellationToken);
        _output.WriteLine($"Results written to {directory}");
        _output.WriteLine("Session comparison so far:");
        _output.WriteLine(await AgentEvaluationReport.WriteSessionSummaryAsync(directory,
            config.ReferenceProviderKey ?? configuredProviderKeys[0], cancellationToken));
    }

    private static List<T> Select<T>(IReadOnlyList<T> defined, string[] selected, Func<T, string> name) =>
        selected.Length == 0
            ? [.. defined]
            : [defined[0], .. defined.Skip(1).Where(v => selected.Contains(name(v), StringComparer.OrdinalIgnoreCase))];

    private static string Truncate(string text) =>
        text.Length <= 160 ? text.ReplaceLineEndings(" ") : $"{text[..160].ReplaceLineEndings(" ")}…";
}

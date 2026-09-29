namespace CasCap.Services;

/// <summary>
/// <see cref="ICommsResponder"/> that answers Signalizr communications with a configured <see cref="AIAgent"/>.
/// </summary>
/// <remarks>
/// <para>
/// The agent is the keyed <see cref="AIAgent"/> named by <see cref="CommsAgentProfile.AgentKey"/>, with its
/// profile and provider in <see cref="AIConfig"/>. When any of them is missing the responder reports itself
/// unavailable, and <see cref="CommunicationsBgService"/> behaves as if no responder were registered.
/// </para>
/// <para>
/// Handles slash commands through <see cref="AgentCommandHandler"/>, poll votes through <see cref="IPollTracker"/>,
/// session persistence, model and instruction overrides, delegation feedback and the stats footer and
/// monitor-group timeline produced by <see cref="CommsDebugNotifier"/>.
/// </para>
/// </remarks>
public sealed partial class AgentCommsResponder : ICommsResponder
{
    private const string Hourglass = "\u23F3";
    private const string TwistedArrows = "\U0001F500";

    private readonly ILogger _logger;
    private readonly IOptions<CommsConfig> _commsConfig;
    private readonly AIConfig _aiConfig;
    private readonly ISignalizrClient _signalizrClient;
    private readonly AgentCommandHandler _commandHandler;
    private readonly IPollTracker _pollTracker;
    private readonly CommsDebugNotifier _debugNotifier;
    private readonly IReadOnlyList<IAgentRunEnricher> _enrichers;
    private readonly AIAgent? _agent;
    private readonly ProviderConfig? _provider;
    private readonly AgentConfig? _agentConfig;
    private readonly string? _resolvedInstructions;

    /// <summary>Initializes a new instance of the <see cref="AgentCommsResponder"/> class.</summary>
    public AgentCommsResponder(ILogger<AgentCommsResponder> logger,
        IOptions<CommsConfig> commsConfig,
        IOptions<AIConfig> aiConfig,
        CommsAgentProfile profile,
        ISignalizrClient signalizrClient,
        AgentCommandHandler commandHandler,
        IPollTracker pollTracker,
        CommsDebugNotifier debugNotifier,
        IEnumerable<IAgentRunEnricher> enrichers,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _commsConfig = commsConfig;
        _aiConfig = aiConfig.Value;
        _signalizrClient = signalizrClient;
        _commandHandler = commandHandler;
        _pollTracker = pollTracker;
        _debugNotifier = debugNotifier;
        _enrichers = [.. enrichers];

        if (_aiConfig.Agents.TryGetValue(profile.AgentKey, out var agentConfig))
        {
            _agentConfig = agentConfig;
            _agent = serviceProvider.GetKeyedService<AIAgent>(profile.AgentKey);
            if (_aiConfig.Providers.TryGetValue(agentConfig.Provider, out var provider))
                _provider = provider;
            _resolvedInstructions = AgentExtensions.ResolveInstructions(agentConfig, profile.InstructionsAssembly, _aiConfig);
        }

        if (!IsAvailable)
            LogAgentNotConfigured(_logger, nameof(AgentCommsResponder), profile.AgentKey);
    }

    /// <inheritdoc/>
    public bool IsAvailable => _agent is not null && _agentConfig is not null && _provider is not null;

    /// <inheritdoc/>
    public string DefaultPrompt => _agentConfig?.Prompt ?? string.Empty;

    /// <inheritdoc/>
    public async Task<CommsTurn> CreateStreamTurnAsync(CommsEvent commsEvent, IReadOnlyList<string>? base64Attachments,
        CancellationToken cancellationToken)
    {
        // Copy the raw event to the monitor group, so it can be read alongside the agent's reply.
        await _debugNotifier.SendStreamEventDebugAsync(commsEvent, cancellationToken);

        var prompt = $"[{commsEvent.Source}] {commsEvent.Message}";
        if (commsEvent.JsonPayload is not null)
            prompt += $"\n\nJSON: {commsEvent.JsonPayload}";
        return new CommsTurn(prompt, ExtraBase64Attachments: base64Attachments);
    }

    /// <inheritdoc/>
    public Task<CommsTurn?> HandlePollVoteAsync(string voter, SignalizrPollVote pollVote, CancellationToken cancellationToken)
    {
        var pollId = pollVote.PollId;
        var selectedIndices = pollVote.OptionIndexes.ToArray();
        LogPollVoteReceived(_logger, nameof(AgentCommsResponder), pollId, string.Join(", ", selectedIndices));

        // Fetch the poll first so we have its metadata even if it expires between RecordVote and
        // building the prompt.
        var poll = _pollTracker.GetPoll(pollId);
        if (!_pollTracker.RecordVote(pollId, voter, selectedIndices) || poll is null)
        {
            LogPollNotTracked(_logger, nameof(AgentCommsResponder), pollId);
            return Task.FromResult<CommsTurn?>(null);
        }

        // Build a descriptive prompt so the agent knows the poll result. The voter's Signal
        // identifier is personal data and stays out of the prompt.
        const string voterName = "A group member";
        var selectedLabels = selectedIndices
            .Where(i => i >= 0 && i < poll.Answers.Length)
            .Select(i => poll.Answers[i]);

        var prompt = $"[POLL VOTE] {voterName} chose \"{string.Join(", ", selectedLabels)}\" "
            + $"on poll \"{poll.Question}\" (ID: {poll.PollId}). "
            + "INSTRUCTIONS: 1) Call close_poll with the ID above. 2) Act on the chosen option. 3) Do NOT present more choices unless they are in a new poll.";
        return Task.FromResult<CommsTurn?>(new CommsTurn(prompt));
    }

    /// <inheritdoc/>
    public async Task<CommsCommandOutcome?> TryHandleCommandAsync(string text, CancellationToken cancellationToken)
    {
        if (!ChatCommandParser.TryParseCommand(text, out var chatCmd, out var cmdArg))
            return null;

        LogSlashCommand(_logger, nameof(AgentCommsResponder), chatCmd);

        // SessionBypass answers the argument as a one-off turn with no conversation history.
        if (chatCmd is ChatCommand.SessionBypass && !string.IsNullOrWhiteSpace(cmdArg))
            return new CommsCommandOutcome(null, new CommsTurn(cmdArg, BypassSession: true));

        var reply = await _commandHandler.HandleCommandAsync(chatCmd, cmdArg, _agent!, _agentConfig!.Name);
        return new CommsCommandOutcome(reply);
    }

    /// <inheritdoc/>
    public async Task<CommsReply?> RespondAsync(CommsTurn turn, CancellationToken cancellationToken)
    {
        var (result, debugSteps) = await RunAgentAsync(turn, cancellationToken);
        if (result is null || string.IsNullOrWhiteSpace(result.OutputText))
            return null;

        var footer = await _debugNotifier.FormatStatsFooterAsync(result, cancellationToken);

        // Convert any image attachments from tool results into Signal base64 attachments.
        var attachments = result.Attachments.Count > 0
            ? result.Attachments
                .Select(a => $"data:{a.MimeType};filename={a.FileName ?? "photo"};base64,{a.Base64Content}")
                .ToArray()
            : null;

        return new CommsReply(result.OutputText, footer, attachments, async ct =>
        {
            // Send the detailed pipeline timeline to the monitor group.
            LogDebugStats(_logger, nameof(AgentCommsResponder),
                result.Usage is not null,
                result.Usage?.InputTokenCount,
                result.Usage?.OutputTokenCount,
                debugSteps.Count,
                debugSteps.Count(s => s.Result is not null),
                debugSteps.Count(s => s.Result?.Usage is not null));
            await _debugNotifier.SendDebugStatsAsync(turn.Prompt, result, debugSteps, turn.Timestamp, ct);
        });
    }

    private async Task<(AgentRunResult? Result, List<CommsDebugStep> DebugSteps)> RunAgentAsync(CommsTurn turn,
        CancellationToken cancellationToken)
    {
        try
        {
            var agent = _agent!;
            var agentConfig = _agentConfig!;
            var provider = _provider!;

            LogAgentInferenceStarting(_logger, nameof(AgentCommsResponder), turn.Prompt.Length,
                turn.BinaryContent is not null, _commandHandler.GetModelOverride(agentConfig.Name) ?? agentConfig.Provider);

            var session = await LoadSessionAsync(agent, agentConfig, turn);

            var message = AgentExtensions.BuildChatMessage(turn.Prompt,
                binaryContent: turn.BinaryContent, mimeType: turn.MimeType);
            var chatOptions = AgentExtensions.BuildChatOptions(agentConfig, _resolvedInstructions!);
            _commandHandler.ApplyModelOverride(chatOptions, agentConfig.Name);
            _commandHandler.ApplyInstructionsOverride(chatOptions, agentConfig.Name, _aiConfig);

            // Accumulate debug steps across the full agent pipeline.
            var debugSteps = new List<CommsDebugStep>();
            var pipelineSw = Stopwatch.StartNew();

            debugSteps.Add(new CommsDebugStep(
                $"\U0001F680 {agentConfig.Name}",
                $"{_commandHandler.GetModelOverride(agentConfig.Name) ?? agentConfig.Provider} ({provider.ModelName})",
                TimeSpan.Zero));

            var runScope = CreateRunScope(turn, debugSteps, pipelineSw, cancellationToken);

            try
            {
                var enrichmentState = new object?[_enrichers.Count];
                for (var i = 0; i < _enrichers.Count; i++)
                    enrichmentState[i] = await _enrichers[i].BeforeRunAsync(cancellationToken);

                var result = await agent.RunAnalysisAsync(
                    provider,
                    agentConfig,
                    message,
                    chatOptions,
                    session: session,
                    cancellationToken: cancellationToken,
                    logger: _logger,
                    scope: runScope);

                for (var i = 0; i < _enrichers.Count; i++)
                    await _enrichers[i].AfterRunAsync(result, enrichmentState[i], cancellationToken);

                LogAgentCompleted(_logger, nameof(AgentCommsResponder), result.Elapsed,
                    result.Session is not null ? "present" : "missing");

                // Persist the updated session so the next call resumes conversation context.
                if (!turn.BypassSession && result.Session is not null)
                {
                    await _commandHandler.SaveSessionAsync(agent, agentConfig.Name, result.Session);
                    LogAgentSessionPersisted(_logger, nameof(AgentCommsResponder));
                }

                // Restore hourglass reaction after delegation completes (Option B cleanup).
                await SetReactionAsync(Hourglass, turn, cancellationToken);

                // Final step for the parent agent.
                pipelineSw.Stop();
                debugSteps.Add(new CommsDebugStep(
                    $"\U0001F3C1 {agentConfig.Name}",
                    null,
                    pipelineSw.Elapsed,
                    result));

                return (result, debugSteps);
            }
            finally
            {
                // Delegation, completion and compaction callbacks live on the run scope and fall out of
                // use with it; only the audio debug artifacts remain ambient.
                AgentExtensions.ClearAmbientAudioDebug();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogAgentInferenceFailed(_logger, ex, nameof(AgentCommsResponder));
            return (null, []);
        }
    }

    /// <summary>Loads the conversation session for the turn, or none when the turn bypasses it.</summary>
    private async Task<AgentSession?> LoadSessionAsync(AIAgent agent, AgentConfig agentConfig, CommsTurn turn)
    {
        if (turn.BypassSession)
        {
            LogAgentSessionBypassed(_logger, nameof(AgentCommsResponder));
            return null;
        }

        var session = await _commandHandler.LoadSessionAsync(agent, agentConfig.Name);
        if (session is null)
            LogAgentSessionStarted(_logger, nameof(AgentCommsResponder));
        else
            LogAgentSessionResumed(_logger, nameof(AgentCommsResponder));
        return session;
    }

    /// <summary>
    /// Builds the per-run scope carrying the host callbacks, so there is no process-wide state to leak if
    /// the run exits early.
    /// </summary>
    private AgentRunScope CreateRunScope(CommsTurn turn, List<CommsDebugStep> debugSteps, Stopwatch pipelineSw,
        CancellationToken cancellationToken) => new()
        {
            OnDelegation = async (agentKey, depth, subProvider, ct) =>
            {
                var depthLabel = depth switch { 1 => "sub-agent", 2 => "sub-sub-agent", _ => $"depth-{depth} agent" };
                LogAgentDelegating(_logger, nameof(AgentCommsResponder), agentKey, depthLabel,
                    $"{subProvider.Type}:{subProvider.ModelName}");

                debugSteps.Add(new CommsDebugStep(
                    $"{TwistedArrows} {agentKey} ({depthLabel})",
                    $"{subProvider.Type}:{subProvider.ModelName}",
                    pipelineSw.Elapsed));

                // Option A: send a separate status message (toggleable via config).
                if (_commsConfig.Value.DelegationMessagesEnabled)
                {
                    await _signalizrClient.SendAsync(_commsConfig.Value.GroupName,
                        $"{TwistedArrows} Consulting {agentKey} ({depthLabel}) \u2022 {subProvider.Type}:{subProvider.ModelName}", ct);
                }

                // Option B: swap reaction to twisted-arrows to indicate delegation.
                await SetReactionAsync(TwistedArrows, turn, ct);
            },

            OnCompletion = (agentKey, depth, subResult, ct) =>
            {
                debugSteps.Add(new CommsDebugStep(
                    $"\u2705 {agentKey}",
                    null,
                    pipelineSw.Elapsed,
                    subResult));
                return Task.CompletedTask;
            },

            OnCompaction = stats =>
            {
                LogSessionCompaction(_logger, nameof(AgentCommsResponder), stats.InputCount, stats.OutputCount,
                    stats.ToolDropped, stats.WindowTrimmed, stats.Target);

                _ = _debugNotifier.SendCompactionDebugAsync(stats.InputCount, stats.OutputCount,
                    stats.ToolDropped, stats.WindowTrimmed, stats.Target, cancellationToken);
            },
        };

    /// <summary>Sets a progress reaction on the turn's inbound message, best effort.</summary>
    private Task SetReactionAsync(string reaction, CommsTurn turn, CancellationToken cancellationToken) =>
        turn is { Sender: { } sender, Timestamp: { } timestamp }
            ? _signalizrClient.TrySetReactionAsync(_commsConfig.Value.GroupName, reaction, timestamp, sender,
                ex => LogGroupInteractionFailed(_logger, ex, nameof(AgentCommsResponder)), cancellationToken)
            : Task.CompletedTask;
}

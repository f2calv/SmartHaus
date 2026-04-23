using Microsoft.ML.Tokenizers;
using ModelContextProtocol.Client;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CasCap.App.Console;

/// <summary>
/// Main console application. Connects to the first <see cref="AgentConfig"/> from
/// <see cref="AIConfig"/> and runs a simple interactive prompt loop with streaming responses.
/// </summary>
public class ConsoleApp(IOptions<AppConfig> appConfig, IOptions<AIConfig> aiConfig, IOptions<ApiAuthConfig> apiAuthConfig, AgentCommandHandler commandHandler, IServiceProvider serviceProvider)
{
    /// <summary>
    /// Approximate tokenizer for input token counting. Uses the <c>cl100k_base</c> encoding
    /// (GPT-4 family) as a cross-model approximation — actual token counts will vary by model.
    /// </summary>
    private static readonly Tokenizer s_tokenizer = TiktokenTokenizer.CreateForEncoding("cl100k_base");

    /// <summary>Prompt history for Up/Down arrow navigation across the session.</summary>
    private static readonly List<string> s_promptHistory = [];

    /// <summary>Collected middleware diagnostic renderables for the current prompt cycle.</summary>
    private static readonly List<IRenderable> s_middlewareLog = [];

    /// <summary>
    /// Runs the main application loop. Pressing Escape clears the console and returns to the
    /// agent selector. Typing exit/quit or cancelling <paramref name="cancellationToken"/> ends the session.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var agents = aiConfig.Value.Agents;

        while (!cancellationToken.IsCancellationRequested)
        {
            AnsiConsole.Clear();

            // ── Agent selector ──────────────────────────────────────────────────
            string selectedKey;
            if (agents.Count == 1)
                selectedKey = agents.Keys.First();
            else
                selectedKey = await AnsiConsole.PromptAsync(
                    new SelectionPrompt<string>()
                        .Title("[blue bold]Select an agent[/]")
                        .PageSize(10)
                        .MoreChoicesText("[grey]Move up/down to reveal more agents[/]")
                        .AddChoices(agents.Keys)
                        .UseConverter(key =>
                        {
                            var a = agents[key];
                            var p = aiConfig.Value.Providers[a.Provider];
                            return $"{Markup.Escape(a.Name)} [grey]({Markup.Escape(p.Type.ToString())} — {Markup.Escape(p.ModelName)})[/]";
                        }));

            var agentConfig = agents[selectedKey];
            var provider = aiConfig.Value.Providers[agentConfig.Provider];


            // ── Gather tools and prompts ────────────────────────────────────
            var tools = AgentExtensions.CreateToolsForAgent(serviceProvider, agentConfig, aiConfig.Value,
                deferResolution: true, isDevelopment: true, instructionsAssembly: typeof(HausServiceCollectionExtensions).Assembly);
            var prompts = AgentExtensions.CreatePromptsForAgent(agentConfig, isDevelopment: true);
            var mcpClients = new List<McpClient>();
            var endpointToolSources = agentConfig.Tools.Where(s => s.Endpoint is not null).ToList();
            var endpointPromptSources = agentConfig.Prompts.Where(s => s.Endpoint is not null).ToList();
            if (endpointToolSources.Count > 0 || endpointPromptSources.Count > 0)
            {
                await AnsiConsole.Status()
                    .StartAsync("Fetching MCP tools and prompts…", async _ =>
                    {
                        foreach (var source in endpointToolSources)
                        {
                            var (mcpClient, mcpTools) = await AgentExtensions.GetHttpTools(source.Endpoint!);
                            mcpClients.Add(mcpClient);
                            tools.AddRange(AgentExtensions.FilterTools(mcpTools.Cast<AITool>(), source,
                                isDevelopment: true));

                            var mcpPrompts = (await mcpClient.ListPromptsAsync()).ToList();
                            prompts.AddRange(mcpPrompts.ToPromptDescriptors());
                        }

                        foreach (var source in endpointPromptSources.Where(s =>
                            !endpointToolSources.Any(t => t.Endpoint == s.Endpoint)))
                        {
                            var (mcpClient, mcpPromptList) = await AgentExtensions.GetHttpPrompts(source.Endpoint!);
                            mcpClients.Add(mcpClient);
                            prompts.AddRange(AgentExtensions.FilterPrompts(
                                mcpPromptList.ToPromptDescriptors(), source, isDevelopment: true));
                        }
                    });
            }


            // ── Create agent ────────────────────────────────────────────────────
            // Infrastructure auth (k8s ingress basic auth) is only needed for Ollama
            // when running outside the cluster. OpenAI auth uses InfraProvider.ApiKey.
            HttpClient? httpClient = null;
            if (provider.Type is not AgentType.OpenAI)
            {
                httpClient = new HttpClient
                {
                    BaseAddress = provider.Endpoint,
                    Timeout = Timeout.InfiniteTimeSpan,
                };
                httpClient.SetBasicAuth(apiAuthConfig.Value.Username, apiAuthConfig.Value.Password);
            }

            var tokenCredential = provider.Type is AgentType.AzureOpenAI
                ? appConfig.Value.TokenCredential
                : null;

            var (chatClient, agent, resolvedInstructions) = AgentExtensions.CreateAgent(provider, agentConfig, httpClient, tools,
                configureChatClient: b => b.Use(
                    getResponseFunc: ChatResponseMiddleware,
                    getStreamingResponseFunc: ChatStreamingResponseMiddleware),
                configureAgent: b => b
                    .Use(AgentRunMiddleware, AgentRunStreamingMiddleware)
                    .Use(FunctionCallingMiddleware),
                instructionsAssembly: typeof(HausServiceCollectionExtensions).Assembly,
                aiConfig: aiConfig.Value,
                tokenCredential: tokenCredential);

            var chatOptions = AgentExtensions.BuildChatOptions(agentConfig, resolvedInstructions);

            try
            {
                AnsiConsole.MarkupLine($"[green]Connected to {Markup.Escape(provider.Type.ToString())} ({Markup.Escape(provider.ModelName)})[/]");
                if (tools.Count > 0)
                {
                    AnsiConsole.MarkupLine($"[blue]{tools.Count} tool(s) available:[/]");
                    foreach (var tool in tools)
                        AnsiConsole.MarkupLine($"  [grey]• {Markup.Escape(tool.Name)} - {Markup.Escape(tool.Description)}[/]");
                }
                else
                    AnsiConsole.MarkupLine("[grey]No tools available.[/]");
                AnsiConsole.WriteLine();

                AgentSession? session = null;

                while (!cancellationToken.IsCancellationRequested)
                {
                    var promptLine = ReadPromptWithTokenCount();

                    if (cancellationToken.IsCancellationRequested)
                        return;

                    if (promptLine is null)
                        break;

                    var command = promptLine.Trim().ToLowerInvariant();
                    if (command is "exit" or "quit" or "")
                        return;

                    AnsiConsole.WriteLine();

                    // ── Slash-command handling ───────────────────────────────────────
                    if (ChatCommandParser.TryParseCommand(promptLine, out var chatCmd, out var cmdArg))
                    {
                        // SessionBypass needs local streaming — handle before delegating.
                        if (chatCmd is ChatCommand.SessionBypass)
                        {
                            if (string.IsNullOrWhiteSpace(cmdArg))
                                AnsiConsole.MarkupLine("[red]Usage: /session bypass <prompt>[/]");
                            else
                                await RunStreamingAndDisplayAsync(agent, agentConfig, provider, chatOptions,
                                    AgentExtensions.BuildChatMessage(cmdArg), bypassSession: null, cancellationToken);
                        }
                        else
                        {
                            // Sync live session to the store so commands see current state.
                            if (session is not null)
                                await commandHandler.SaveSessionAsync(agent, agentConfig.Name, session);

                            var response = await commandHandler.HandleCommandAsync(
                                chatCmd, cmdArg, agent, agentConfig.Name);

                            if (response is not null)
                                AnsiConsole.MarkupLine($"[green]{Markup.Escape(response)}[/]");

                            // Reload session from store (may have been reset or compacted).
                            session = await commandHandler.LoadSessionAsync(agent, agentConfig.Name);

                            // Keep chatOptions in sync with model and instructions overrides.
                            commandHandler.ApplyModelOverride(chatOptions);
                            commandHandler.ApplyInstructionsOverride(chatOptions, aiConfig.Value);
                        }

                        AnsiConsole.WriteLine();
                        continue;
                    }

                    lock (s_middlewareLog)
                        s_middlewareLog.Clear();

                    var message = AgentExtensions.BuildChatMessage(promptLine);
                    var result = new AgentRunResult(agentConfig.Name);
                    var sw = Stopwatch.StartNew();

                    try
                    {
                        session ??= await agent.CreateSessionAsync(cancellationToken).AsTask();
                        AgentRunOptions runOptions = new ChatClientAgentRunOptions(chatOptions);

                        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

                        var enumerator = agent.RunStreamingAsync(
                            [message], session, runOptions, timeoutCts.Token).GetAsyncEnumerator(timeoutCts.Token);

                        // ── Spinner until first text token ───────────────────────
                        await AnsiConsole.Status()
                            .Spinner(Spinner.Known.Dots)
                            .StartAsync($"Waiting for {Markup.Escape(provider.ModelName)}…", async _ =>
                            {
                                while (await enumerator.MoveNextAsync())
                                {
                                    var update = enumerator.Current;
                                    ProcessUpdate(update, result, sw);

                                    if (result.TimeToFirstToken.HasValue)
                                        break;
                                }
                            });

                        // ── Stream remaining tokens ─────────────────────────────
                        while (await enumerator.MoveNextAsync())
                            ProcessUpdate(enumerator.Current, result, sw);
                    }
                    catch (OperationCanceledException)
                    {
                        result.AppendText("(cancelled)");
                    }
                    catch (Exception ex)
                    {
                        result.Error = ex;
                    }

                    result.Elapsed = sw.Elapsed;
                    result.IsComplete = result.Error is null;
                    result.Session = session;
                    AnsiConsole.WriteLine();
                    AnsiConsole.WriteLine();

                    if (result.Error is not null)
                        AnsiConsole.WriteException(result.Error, ExceptionFormats.ShortenEverything);

                    RenderSummary(provider, agentConfig, result);
                }
            }
            finally
            {
                foreach (var mcpClient in mcpClients)
                    await mcpClient.DisposeAsync();
                chatClient.Dispose();
            }
        }
    }

    /// <summary>
    /// Reads a line of input character-by-character, displaying a live approximate token count
    /// (via <see cref="s_tokenizer"/>) before the prompt on every keypress.
    /// </summary>
    /// <returns>The entered text, or <see langword="null"/> if the user presses Escape.</returns>
    private static string? ReadPromptWithTokenCount()
    {
        var input = new StringBuilder();
        var cursorPos = 0;
        var historyIndex = s_promptHistory.Count;
        var prevLineLen = 0;
        RenderPromptLine(input, 0, cursorPos, ref prevLineLen);

        while (true)
        {
            var key = System.Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    AnsiConsole.WriteLine();
                    var line = input.ToString();
                    if (!string.IsNullOrWhiteSpace(line))
                        s_promptHistory.Add(line);
                    return line;

                case ConsoleKey.Escape:
                    AnsiConsole.Write($"\r{new string(' ', prevLineLen)}\r");
                    return null;

                case ConsoleKey.Backspace:
                    if (cursorPos > 0)
                    {
                        input.Remove(cursorPos - 1, 1);
                        cursorPos--;
                    }
                    break;

                case ConsoleKey.Delete:
                    if (cursorPos < input.Length)
                        input.Remove(cursorPos, 1);
                    break;

                case ConsoleKey.LeftArrow:
                    if ((key.Modifiers & ConsoleModifiers.Control) != 0)
                        cursorPos = FindPreviousWordBoundary(input, cursorPos);
                    else if (cursorPos > 0)
                        cursorPos--;
                    break;

                case ConsoleKey.RightArrow:
                    if ((key.Modifiers & ConsoleModifiers.Control) != 0)
                        cursorPos = FindNextWordBoundary(input, cursorPos);
                    else if (cursorPos < input.Length)
                        cursorPos++;
                    break;

                case ConsoleKey.Home:
                    cursorPos = 0;
                    break;

                case ConsoleKey.End:
                    cursorPos = input.Length;
                    break;

                case ConsoleKey.UpArrow:
                    if (historyIndex > 0)
                    {
                        historyIndex--;
                        input.Clear().Append(s_promptHistory[historyIndex]);
                        cursorPos = input.Length;
                    }
                    break;

                case ConsoleKey.DownArrow:
                    if (historyIndex < s_promptHistory.Count - 1)
                    {
                        historyIndex++;
                        input.Clear().Append(s_promptHistory[historyIndex]);
                        cursorPos = input.Length;
                    }
                    else if (historyIndex < s_promptHistory.Count)
                    {
                        historyIndex = s_promptHistory.Count;
                        input.Clear();
                        cursorPos = 0;
                    }
                    break;

                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        input.Insert(cursorPos, key.KeyChar);
                        cursorPos++;
                    }
                    else
                        continue;
                    break;
            }

            var tokens = input.Length > 0 ? s_tokenizer.CountTokens(input.ToString()) : 0;
            RenderPromptLine(input, tokens, cursorPos, ref prevLineLen);
        }
    }

    /// <summary>
    /// Redraws the prompt line in the format <c>[~N] &gt; input</c> with coloured segments,
    /// positioning the terminal cursor at <paramref name="cursorPos"/> within the input.
    /// </summary>
    private static void RenderPromptLine(StringBuilder input, int tokenCount, int cursorPos, ref int prevLineLen)
    {
        var tokenPart = $"[~{tokenCount}]";
        var plainText = $"{tokenPart} > {input}";
        var currentLen = plainText.Length;
        var padding = currentLen < prevLineLen ? new string(' ', prevLineLen - currentLen) : string.Empty;

        // Use raw Console.Write with \r to overwrite the current line in-place.
        // AnsiConsole.Markup can misbehave with \r in some terminal emulators.
        System.Console.Write($"\r\x1b[90m{tokenPart}\x1b[0m \x1b[34m>\x1b[0m {input}{padding}");

        // Move cursor back from end-of-line to the desired position within the input
        var moveBack = input.Length + padding.Length - cursorPos;
        if (moveBack > 0)
            System.Console.Write($"\x1b[{moveBack}D");

        prevLineLen = currentLen;
    }

    /// <summary>Finds the start of the previous word (skips whitespace then word characters).</summary>
    private static int FindPreviousWordBoundary(StringBuilder input, int cursorPos)
    {
        if (cursorPos <= 0)
            return 0;

        var pos = cursorPos - 1;

        while (pos > 0 && char.IsWhiteSpace(input[pos]))
            pos--;

        while (pos > 0 && !char.IsWhiteSpace(input[pos - 1]))
            pos--;

        return pos;
    }

    /// <summary>Finds the start of the next word (skips word characters then whitespace).</summary>
    private static int FindNextWordBoundary(StringBuilder input, int cursorPos)
    {
        if (cursorPos >= input.Length)
            return input.Length;

        var pos = cursorPos;

        while (pos < input.Length && !char.IsWhiteSpace(input[pos]))
            pos++;

        while (pos < input.Length && char.IsWhiteSpace(input[pos]))
            pos++;

        return pos;
    }

    /// <summary>
    /// Processes a single <see cref="AgentResponseUpdate"/>: extracts text, writes it to the console,
    /// and captures metadata/usage on the <paramref name="result"/>.
    /// </summary>
    private static void ProcessUpdate(AgentResponseUpdate update, AgentRunResult result, Stopwatch sw)
    {
        result.UpdateCount++;

        // ── Thinking / reasoning content (TextReasoningContent) ─────────
        if (update.Contents is { Count: > 0 })
        {
            var thinkingText = string.Concat(update.Contents
                .OfType<TextReasoningContent>()
                .Select(tc => tc.Text));

            if (!string.IsNullOrEmpty(thinkingText))
            {
                result.TimeToFirstToken ??= sw.Elapsed;
                result.ThinkingUpdateCount++;
                result.AppendThinkingText(thinkingText);

                AnsiConsole.Markup($"[grey]{Markup.Escape(thinkingText)}[/]");
            }
        }

        // ── Regular text content ────────────────────────────────────────
        var text = update.Text;
        if (string.IsNullOrEmpty(text) && update.Contents is { Count: > 0 })
        {
            text = string.Concat(update.Contents
                .OfType<TextContent>()
                .Select(tc => tc.Text));
        }

        if (!string.IsNullOrEmpty(text))
        {
            // Fallback: detect <think>/<​/think> tags for providers that emit
            // reasoning as plain text instead of TextReasoningContent.
            if (text.Contains("<think>", StringComparison.Ordinal))
            {
                result.IsThinking = true;
                text = text.Replace("<think>", string.Empty);
            }

            if (text.Contains("</think>", StringComparison.Ordinal))
            {
                result.IsThinking = false;
                var closeIdx = text.IndexOf("</think>", StringComparison.Ordinal);
                var thinkPart = text[..closeIdx];
                text = text[(closeIdx + "</think>".Length)..];

                if (!string.IsNullOrEmpty(thinkPart))
                {
                    result.ThinkingUpdateCount++;
                    result.AppendThinkingText(thinkPart);
                    AnsiConsole.Markup($"[grey]{Markup.Escape(thinkPart)}[/]");
                }
            }

            if (result.IsThinking)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    result.TimeToFirstToken ??= sw.Elapsed;
                    result.ThinkingUpdateCount++;
                    result.AppendThinkingText(text);
                    AnsiConsole.Markup($"[grey]{Markup.Escape(text)}[/]");
                }
            }
            else if (!string.IsNullOrEmpty(text))
            {
                if (result.ThinkingUpdateCount > 0 && result.TextUpdateCount == 0)
                    AnsiConsole.WriteLine();

                result.TimeToFirstToken ??= sw.Elapsed;

                result.TextUpdateCount++;
                result.AppendText(text);
                AnsiConsole.Write(text);
            }
        }

        // Capture metadata from every update (last write wins)
        result.ResponseId ??= update.ResponseId;
        result.CreatedAt ??= update.CreatedAt;

        if (update.FinishReason is not null)
            result.FinishReason = update.FinishReason?.Value ?? string.Empty;

        // Collect usage from UsageContent items
        if (update.Contents is { Count: > 0 })
        {
            foreach (var uc in update.Contents.OfType<UsageContent>())
            {
                if (uc.Details is not null)
                    result.Usage = uc.Details;
                if (uc.AdditionalProperties is { Count: > 0 })
                {
                    foreach (var kv in uc.AdditionalProperties)
                        result.AdditionalProperties[kv.Key] = kv.Value;
                }
            }

            var functionCalls = update.Contents.OfType<FunctionCallContent>().ToList();
            result.ToolCallCount += functionCalls.Count;
            result.ToolCalls.AddRange(functionCalls.Select(f => new ToolCallInfo(f.Name, f.Arguments)));
        }

        // Collect update-level additional properties
        if (update.AdditionalProperties is { Count: > 0 })
        {
            foreach (var kv in update.AdditionalProperties)
                result.AdditionalProperties[kv.Key] = kv.Value;
        }
    }

    /// <summary>
    /// Renders a 50:50 two-column summary: left column shows a unified
    /// <see cref="ProviderConfig"/> / <see cref="AgentConfig"/> / <see cref="AgentRunResult"/> table,
    /// right column shows buffered middleware diagnostics.
    /// </summary>
    private static void RenderSummary(ProviderConfig provider, AgentConfig agentConfig, AgentRunResult result)
    {
        // ── Single summary table with section headers ───────────────────
        var summaryTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderStyle(Style.Parse("grey"))
            .Expand()
            .AddColumn(new TableColumn("[grey]Property[/]").NoWrap())
            .AddColumn(new TableColumn("[grey]Value[/]"));

        // ── Provider ────────────────────────────────────────────────────
        summaryTable.AddRow("[blue bold]Provider[/]", "");
        summaryTable.AddRow("Type", Markup.Escape(provider.Type.ToString()));
        summaryTable.AddRow("Model", Markup.Escape(provider.ModelName));
        summaryTable.AddRow("Endpoint", Markup.Escape(provider.Endpoint?.ToString() ?? "—"));

        // ── Agent ───────────────────────────────────────────────────────
        summaryTable.AddEmptyRow();
        summaryTable.AddRow("[blue bold]Agent[/]", "");
        summaryTable.AddRow("Name", Markup.Escape(agentConfig.Name));
        summaryTable.AddRow("Description", Markup.Escape(agentConfig.Description));
        if (provider.ReasoningEffort.HasValue)
            summaryTable.AddRow("Reasoning Effort", Markup.Escape(provider.ReasoningEffort.Value.ToString()));
        var serviceSources = agentConfig.Tools.Where(s => s.Service is not null).ToList();
        var endpointSources = agentConfig.Tools.Where(s => s.Endpoint is not null).ToList();
        if (endpointSources.Count > 0)
            summaryTable.AddRow("Tool Endpoints", $"{endpointSources.Count}");
        if (serviceSources.Count > 0)
            summaryTable.AddRow("Tool Services", Markup.Escape(string.Join(", ", serviceSources.Select(s => s.Service!))));

        // ── Status ──────────────────────────────────────────────────────
        summaryTable.AddEmptyRow();
        summaryTable.AddRow("[blue bold]Status[/]", "");
        summaryTable.AddRow("Status", result.Error is not null ? "[red]Failed[/]" : result.IsComplete ? "[green]Complete[/]" : "[yellow]Incomplete[/]");
        summaryTable.AddRow("Finish Reason", Markup.Escape(result.FinishReason));
        summaryTable.AddRow("Session", result.Session is not null ? "[green]Active[/]" : "[grey]None[/]");
        if (result.ResponseId is not null)
            summaryTable.AddRow("Response ID", Markup.Escape(result.ResponseId));
        if (result.CreatedAt.HasValue)
            summaryTable.AddRow("Created At", Markup.Escape(result.CreatedAt.Value.ToString("O")));

        // ── Timing ──────────────────────────────────────────────────────
        summaryTable.AddEmptyRow();
        summaryTable.AddRow("[blue bold]Timing[/]", "");
        summaryTable.AddRow("Total Time", $"{result.Elapsed.TotalSeconds:F2} s");
        summaryTable.AddRow("Time to First Token", result.TimeToFirstToken.HasValue
            ? $"{result.TimeToFirstToken.Value.TotalMilliseconds:F0} ms"
            : "—");

        if (result.TimeToFirstToken.HasValue && result.Usage?.OutputTokenCount is > 0)
        {
            var generationTime = result.Elapsed - result.TimeToFirstToken.Value;
            var tokensPerSecond = result.Usage.OutputTokenCount.Value / generationTime.TotalSeconds;
            summaryTable.AddRow("Tokens/sec (output)", $"{tokensPerSecond:F1}");
        }

        // ── Streaming ───────────────────────────────────────────────────
        summaryTable.AddEmptyRow();
        summaryTable.AddRow("[blue bold]Streaming[/]", "");
        summaryTable.AddRow("Updates", $"{result.UpdateCount}");
        summaryTable.AddRow("Text Updates", $"{result.TextUpdateCount}");
        summaryTable.AddRow("Output Length", $"{result.OutputText.Length:N0} chars");
        if (result.ToolCallCount > 0)
            summaryTable.AddRow("Tool Calls", $"{result.ToolCallCount}");

        if (result.ThinkingUpdateCount > 0)
        {
            summaryTable.AddRow("Thinking Updates", $"{result.ThinkingUpdateCount}");
            summaryTable.AddRow("Thinking Length", $"{result.ThinkingText.Length:N0} chars");
        }

        // ── Tokens ──────────────────────────────────────────────────────
        if (result.Usage is not null)
        {
            summaryTable.AddEmptyRow();
            summaryTable.AddRow("[blue bold]Tokens[/]", "");
            summaryTable.AddRow("Input", result.Usage.InputTokenCount?.ToString("N0") ?? "—");
            summaryTable.AddRow("Output", result.Usage.OutputTokenCount?.ToString("N0") ?? "—");
            summaryTable.AddRow("Total", result.Usage.TotalTokenCount?.ToString("N0") ?? "—");
            if (result.Usage.ReasoningTokenCount is > 0)
                summaryTable.AddRow("Reasoning", result.Usage.ReasoningTokenCount.Value.ToString("N0"));

            if (result.Usage.AdditionalCounts is { Count: > 0 })
            {
                foreach (var kv in result.Usage.AdditionalCounts)
                    summaryTable.AddRow(Markup.Escape(kv.Key), $"{kv.Value:N0}");
            }
        }

        // ── Additional Properties ───────────────────────────────────────
        if (result.AdditionalProperties.Count > 0)
        {
            summaryTable.AddEmptyRow();
            summaryTable.AddRow("[blue bold]Additional[/]", "");
            foreach (var kv in result.AdditionalProperties)
                summaryTable.AddRow(Markup.Escape(kv.Key), Markup.Escape(kv.Value?.ToString() ?? "—"));
        }

        // ── Middleware panel ────────────────────────────────────────────
        IRenderable middlewareContent;
        lock (s_middlewareLog)
        {
            middlewareContent = s_middlewareLog.Count > 0
                ? new Rows(s_middlewareLog.ToArray())
                : new Markup("[grey]No middleware output.[/]");
        }
        var middlewarePanel = new Panel(middlewareContent)
            .Header("[blue]Middleware[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("grey"))
            .Expand();

        // ── 50:50 two-column layout ─────────────────────────────────────
        var columnWidth = (AnsiConsole.Profile.Width - 2) / 2;
        var container = new Table()
            .Border(TableBorder.None)
            .Expand()
            .AddColumn(new TableColumn(string.Empty).Width(columnWidth).PadLeft(0).PadRight(1))
            .AddColumn(new TableColumn(string.Empty).Width(columnWidth).PadLeft(1).PadRight(0));
        container.ShowHeaders = false;
        container.AddRow(summaryTable, middlewarePanel);

        AnsiConsole.Write(container);
        AnsiConsole.WriteLine();
    }

    #region commands

    /// <summary>
    /// Runs the agent against <paramref name="message"/> using <paramref name="bypassSession"/>
    /// (typically <see langword="null"/> to bypass the active session) and streams the response
    /// to the console. The caller's session is not modified.
    /// </summary>
    private static async Task RunStreamingAndDisplayAsync(
        AIAgent agent,
        AgentConfig agentConfig,
        ProviderConfig provider,
        ChatOptions chatOptions,
        ChatMessage message,
        AgentSession? bypassSession,
        CancellationToken cancellationToken)
    {
        var result = new AgentRunResult(agentConfig.Name);
        var sw = Stopwatch.StartNew();

        try
        {
            AgentRunOptions runOptions = new ChatClientAgentRunOptions(chatOptions);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

            var enumerator = agent.RunStreamingAsync(
                [message], bypassSession, runOptions, timeoutCts.Token).GetAsyncEnumerator(timeoutCts.Token);

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"Waiting for {Markup.Escape(provider.ModelName)}…", async _ =>
                {
                    while (await enumerator.MoveNextAsync())
                    {
                        ProcessUpdate(enumerator.Current, result, sw);
                        if (result.TimeToFirstToken.HasValue)
                            break;
                    }
                });

            while (await enumerator.MoveNextAsync())
                ProcessUpdate(enumerator.Current, result, sw);
        }
        catch (OperationCanceledException)
        {
            result.AppendText("(cancelled)");
        }
        catch (Exception ex)
        {
            result.Error = ex;
        }

        result.Elapsed = sw.Elapsed;
        result.IsComplete = result.Error is null;
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        if (result.Error is not null)
            AnsiConsole.WriteException(result.Error, ExceptionFormats.ShortenEverything);
    }

    #endregion

    #region middleware

    /// <summary>
    /// Chat-client-level response middleware that logs message counts before and after the inner client processes the request.
    /// </summary>
    internal static async Task<ChatResponse> ChatResponseMiddleware(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerClient,
        CancellationToken cancellationToken)
    {
        LogOutgoingMessages(nameof(ChatResponseMiddleware), messages, options);
        var response = await innerClient.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        LogMiddleware(new Markup($"[grey]{nameof(ConsoleApp)} chat response message count={response.Messages.Count}[/]"));
        return response;
    }

    /// <summary>
    /// Chat-client-level streaming response middleware that logs message counts before and after the inner client streams the response.
    /// </summary>
    internal static async IAsyncEnumerable<ChatResponseUpdate> ChatStreamingResponseMiddleware(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerClient,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        LogOutgoingMessages(nameof(ChatStreamingResponseMiddleware), messages, options);
        List<ChatResponseUpdate> updates = [];
        await foreach (var update in innerClient.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            updates.Add(update);
            yield return update;
        }
        LogMiddleware(new Markup($"[grey]{nameof(ConsoleApp)} streaming chat update count={updates.Count}[/]"));
    }

    /// <summary>
    /// Agent run middleware that logs message counts before and after the inner agent processes the request.
    /// </summary>
    /// <remarks>
    /// See <see href="https://learn.microsoft.com/en-us/agent-framework/agents/middleware/?pivots=programming-language-csharp">Agent middleware documentation</see>.
    /// </remarks>
    internal static async Task<AgentResponse> AgentRunMiddleware(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        LogMiddleware(new Markup($"[grey]{nameof(ConsoleApp)} agent message count={messages.Count()}[/]"));
        var response = await innerAgent.RunAsync(messages, session, options, cancellationToken).ConfigureAwait(false);
        LogMiddleware(new Markup($"[grey]{nameof(ConsoleApp)} agent response message count={response.Messages.Count}[/]"));
        return response;
    }

    /// <summary>
    /// Agent run streaming middleware that logs message counts before and after the inner agent streams the response.
    /// </summary>
    internal static async IAsyncEnumerable<AgentResponseUpdate> AgentRunStreamingMiddleware(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        LogMiddleware(new Markup($"[grey]{nameof(ConsoleApp)} streaming agent message count={messages.Count()}[/]"));
        List<AgentResponseUpdate> updates = [];
        await foreach (var update in innerAgent.RunStreamingAsync(messages, session, options, cancellationToken))
        {
            updates.Add(update);
            yield return update;
        }
        LogMiddleware(new Markup($"[grey]{nameof(ConsoleApp)} streaming agent update count={updates.ToAgentResponse().Messages.Count}[/]"));
    }

    /// <summary>
    /// Function calling middleware that logs the function name, arguments and result for each tool invocation.
    /// When the result contains a <c>bytes</c> property (base64-encoded image), a <see cref="CanvasImage"/>
    /// is added to the middleware buffer and the raw bytes are excluded from the JSON panel.
    /// All output is collected as <see cref="IRenderable"/> via <see cref="LogMiddleware"/> so it can be
    /// positioned by the layout in <see cref="RenderSummary"/>.
    /// </summary>
    internal static async ValueTask<object?> FunctionCallingMiddleware(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        var argsText = context.Arguments.Count > 0
            ? $" (Args: {string.Join(", ", context.Arguments.Select(x => $"[{x.Key} = {x.Value}]"))})"
            : string.Empty;
        LogMiddleware(new Markup($"[yellow]Tool Call: '{Markup.Escape(context.Function.Name)}'{Markup.Escape(argsText)}[/]"));

        var result = await next(context, cancellationToken);

        // MCP remote tools return the result as a raw JSON string; serializing that
        // would double-escape it. Detect valid JSON strings and use them directly.
        // Non-string MCP types (e.g. CallToolResult) also produce clean JSON via ToString().
        var resultJson = result switch
        {
            null => "null",
            string s when IsValidJson(s) => s,
            _ when result.ToString() is { } raw && IsValidJson(raw) => raw,
            _ => JsonSerializer.Serialize(result, s_jsonOptions),
        };

        // Detect base64-encoded image bytes in the result and render as a CanvasImage renderable
        if (result is not null && TryExtractImageBytes(resultJson, out var imageBytes))
        {
            LogMiddleware(BuildImageRenderable(imageBytes, context.Function.Name));

            var summaryJson = TruncateBytesProperty(resultJson);
            LogMiddleware(new Panel(new JsonText(summaryJson))
                .Header($"[yellow]Tool Result: {Markup.Escape(context.Function.Name)}[/]")
                .Border(BoxBorder.Rounded)
                .BorderStyle(Style.Parse("grey")));
        }
        else
        {
            LogMiddleware(new Panel(new JsonText(TruncateBytesProperty(resultJson)))
                .Header($"[yellow]Tool Result: {Markup.Escape(context.Function.Name)}[/]")
                .Border(BoxBorder.Rounded)
                .BorderStyle(Style.Parse("grey")));
        }

        return result;
    }

    /// <summary>
    /// Builds an <see cref="IRenderable"/> from raw image bytes. Returns a <see cref="CanvasImage"/>
    /// wrapped in a <see cref="Panel"/> on success, or a red error <see cref="Markup"/> on failure.
    /// </summary>
    private static IRenderable BuildImageRenderable(byte[] imageBytes, string toolName)
    {
        try
        {
            var canvasImage = new CanvasImage(imageBytes);
            // Leave MaxWidth unset — the containing panel/column constrains the width automatically
            return new Panel(canvasImage)
                .Header($"[yellow]{Markup.Escape(toolName)}[/] [grey](image)[/]")
                .Border(BoxBorder.Rounded)
                .BorderStyle(Style.Parse("grey"));
        }
        catch (Exception ex)
        {
            return new Markup($"[red]Image render failed: {Markup.Escape(ex.Message)}[/]");
        }
    }

    /// <summary>JSON serializer options for rendering middleware diagnostics.</summary>
    private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

    /// <summary>
    /// Appends a renderable to the middleware diagnostic buffer for display in the summary right column.
    /// </summary>
    private static void LogMiddleware(IRenderable renderable)
    {
        lock (s_middlewareLog)
            s_middlewareLog.Add(renderable);
    }

    /// <summary>
    /// Logs the outgoing <see cref="ChatMessage"/> collection and <see cref="ChatOptions"/> as
    /// syntax-highlighted JSON panels using <see cref="JsonText"/> to the middleware buffer.
    /// </summary>
    private static void LogOutgoingMessages(string middlewareName, IEnumerable<ChatMessage> messages, ChatOptions? options = null)
    {
        var messageList = messages.ToList();
        var payload = messageList.Select(m => new
        {
            role = m.Role.Value,
            contents = m.Contents?.Select<AIContent, object>(c => c switch
            {
                TextContent tc => new { type = "text", text = tc.Text },
                FunctionCallContent fc => new { type = "function_call", name = fc.Name, arguments = fc.Arguments },
                FunctionResultContent fr => new { type = "function_result", callId = fr.CallId, result = FormatFunctionResult(fr.Result) },
                _ => new { type = c.GetType().Name, text = c.ToString() ?? string.Empty } as object,
            }).ToList(),
        }).ToList();

        var json = JsonSerializer.Serialize(payload, s_jsonOptions);
        LogMiddleware(new Panel(new JsonText(json))
            .Header($"[blue]{Markup.Escape(middlewareName)}[/] [grey]({messageList.Count} messages)[/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(Style.Parse("grey")));

        if (options is not null)
        {
            var optionsPayload = new Dictionary<string, object?>
            {
                ["modelId"] = options.ModelId,
                ["temperature"] = options.Temperature,
                ["topP"] = options.TopP,
                ["topK"] = options.TopK,
                ["maxOutputTokens"] = options.MaxOutputTokens,
                ["stopSequences"] = options.StopSequences,
                ["seed"] = options.Seed,
                ["frequencyPenalty"] = options.FrequencyPenalty,
                ["presencePenalty"] = options.PresencePenalty,
            };

            if (options.AdditionalProperties is { Count: > 0 })
            {
                foreach (var kv in options.AdditionalProperties)
                    optionsPayload[kv.Key] = kv.Value;
            }

            // Remove null entries to keep the output concise
            var filtered = optionsPayload
                .Where(kv => kv.Value is not null)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            if (filtered.Count > 0)
            {
                var optionsJson = JsonSerializer.Serialize(filtered, s_jsonOptions);
                LogMiddleware(new Panel(new JsonText(optionsJson))
                    .Header($"[blue]{Markup.Escape(middlewareName)}[/] [grey](ChatOptions)[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderStyle(Style.Parse("grey")));
            }
        }
    }

    /// <summary>
    /// Formats a function result for middleware display: parses JSON results, truncates any
    /// <c>bytes</c> property, and returns a <see cref="JsonElement"/> so the outer serializer
    /// embeds it as proper nested JSON rather than an escaped string.
    /// </summary>
    private static object? FormatFunctionResult(object? result)
    {
        var raw = result?.ToString();
        if (string.IsNullOrEmpty(raw))
            return raw;

        try
        {
            var truncated = TruncateBytesProperty(raw);
            using var doc = JsonDocument.Parse(truncated);
            return doc.RootElement.Clone();
        }
        catch
        {
            return raw;
        }
    }

    /// <summary>
    /// Attempts to extract a base64-encoded <c>bytes</c> property from serialized JSON,
    /// returning the decoded image bytes when present and non-empty.
    /// </summary>
    private static bool TryExtractImageBytes(string json, out byte[] imageBytes)
    {
        imageBytes = [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind is not JsonValueKind.Object)
                return false;

            if (!doc.RootElement.TryGetProperty("bytes", out var bytesElement)
                || bytesElement.ValueKind is not JsonValueKind.String)
                return false;

            imageBytes = bytesElement.GetBytesFromBase64();
            return imageBytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns a copy of the JSON string with any <c>bytes</c> string property truncated
    /// to 255 characters (with an ellipsis suffix) to keep middleware panel output concise.
    /// </summary>
    private static string TruncateBytesProperty(string json)
    {
        const int maxLen = 255;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var filtered = new Dictionary<string, object?>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name is "bytes" && prop.Value.ValueKind is JsonValueKind.String)
                {
                    var raw = prop.Value.GetString() ?? string.Empty;
                    filtered[prop.Name] = raw.Length > maxLen
                        ? $"{raw[..maxLen]}..."
                        : raw;
                    continue;
                }
                filtered[prop.Name] = prop.Value.Clone();
            }
            return JsonSerializer.Serialize(filtered, s_jsonOptions);
        }
        catch
        {
            return json;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> is a syntactically valid
    /// JSON object or array. Used to avoid double-serializing results from MCP remote tools
    /// which already return raw JSON strings.
    /// </summary>
    private static bool IsValidJson(string value)
    {
        var trimmed = value.AsSpan().Trim();
        if (trimmed.Length < 2)
            return false;
        if (trimmed[0] is not ('{' or '['))
            return false;
        try
        {
            using var doc = JsonDocument.Parse(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}

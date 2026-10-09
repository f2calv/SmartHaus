using Microsoft.ML.Tokenizers;

namespace CasCap.App.Console;

/// <summary>Interactive terminal client for tenant-scoped agents hosted by the remote Agent Runtime.</summary>
public sealed class ConsoleApp(
    IOptions<AgentRuntimeConsoleConfig> consoleConfig,
    IAgentRuntimeClient agentRuntimeClient)
{
    private static readonly Tokenizer s_tokenizer = TiktokenTokenizer.CreateForEncoding("cl100k_base");
    private static readonly List<string> s_promptHistory = [];

    /// <summary>Runs the interactive agent selection and prompt loop.</summary>
    /// <param name="cancellationToken">Cancels the console session.</param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            AnsiConsole.Clear();
            var agentName = await SelectAgentAsync(cancellationToken);
            if (agentName is null)
                return;

            if (!await RunAgentLoopAsync(agentName, cancellationToken))
                return;
        }
    }

    private Task<string?> SelectAgentAsync(CancellationToken cancellationToken)
    {
        var agentNames = consoleConfig.Value.AgentNames;
        if (agentNames.Length == 1)
            return Task.FromResult<string?>(agentNames[0]);

        return AnsiConsole.PromptAsync(
            new SelectionPrompt<string>()
                .Title("[blue bold]Select an agent[/]")
                .PageSize(10)
                .MoreChoicesText("[grey]Move up/down to reveal more agents[/]")
                .AddChoices(agentNames),
            cancellationToken)!;
    }

    private async Task<bool> RunAgentLoopAsync(string agentName, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"[green]Connected to remote agent {Markup.Escape(agentName)}[/]");
        AnsiConsole.MarkupLine($"[grey]Session: {Markup.Escape(consoleConfig.Value.SessionId)}[/]");
        AnsiConsole.WriteLine();

        while (!cancellationToken.IsCancellationRequested)
        {
            var prompt = ReadPromptWithTokenCount();
            if (cancellationToken.IsCancellationRequested)
                return false;
            if (prompt is null)
                return true;

            var command = prompt.Trim();
            if (command.Length == 0 || command.Equals("exit", StringComparison.OrdinalIgnoreCase)
                || command.Equals("quit", StringComparison.OrdinalIgnoreCase))
                return false;

            if (command.StartsWith('/'))
            {
                AnsiConsole.MarkupLine("[red]Commands are not supported by the remote console.[/]");
                continue;
            }

            await RunTurnAsync(agentName, prompt, cancellationToken);
        }

        return false;
    }

    private async Task RunTurnAsync(string agentName, string prompt, CancellationToken cancellationToken)
    {
        RunAgentResponse? response = null;
        List<RunAgentEvent> events = [];

        try
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"Waiting for {Markup.Escape(agentName)}...", async status =>
                {
                    await foreach (var item in agentRuntimeClient.StreamAgentAsync(
                        agentName,
                        new RunAgentRequest
                        {
                            SessionId = consoleConfig.Value.SessionId,
                            Input = prompt,
                            IncludeDiagnosticDetails = consoleConfig.Value.DiagnosticDetailsEnabled,
                        },
                        cancellationToken))
                    {
                        if (item.Event is { } runtimeEvent)
                        {
                            events.Add(runtimeEvent);
                            status.Status(BuildEventStatus(runtimeEvent));
                        }

                        response = item.Response ?? response;
                    }
                });

            if (response is null)
                throw new InvalidDataException("Agent Runtime completed the stream without a final response.");

            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine(response.OutputText);
            AnsiConsole.WriteLine();
            RenderSummary(agentName, response, events);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AnsiConsole.WriteException(exception, ExceptionFormats.ShortenEverything);
        }
    }

    private static string BuildEventStatus(RunAgentEvent runtimeEvent)
    {
        var agent = string.IsNullOrWhiteSpace(runtimeEvent.AgentName)
            ? string.Empty
            : $" {Markup.Escape(runtimeEvent.AgentName)}";
        return $"{Markup.Escape(runtimeEvent.Type)}{agent}";
    }

    private static void RenderSummary(
        string agentName,
        RunAgentResponse response,
        List<RunAgentEvent> events)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderStyle(Style.Parse("grey"))
            .AddColumn(new TableColumn("[grey]Property[/]").NoWrap())
            .AddColumn(new TableColumn("[grey]Value[/]"));

        table.AddRow("Agent", Markup.Escape(agentName));
        table.AddRow("Definition", Markup.Escape(response.DefinitionVersion));
        table.AddRow("Model", Markup.Escape(response.ModelName));
        table.AddRow("Finish reason", Markup.Escape(response.FinishReason ?? "-"));
        table.AddRow("Elapsed", $"{response.ElapsedMilliseconds / 1000:F2} s");
        table.AddRow("Time to first token", response.TimeToFirstTokenMilliseconds is { } firstToken
            ? $"{firstToken:F0} ms"
            : "-");
        table.AddRow("Session", response.Session?.Exists is true ? "[green]Persisted[/]" : "[grey]Empty[/]");
        table.AddRow("Events", events.Count.ToString());
        table.AddRow("Tool calls", response.ToolCalls.Count.ToString());
        table.AddRow("Attachments", response.Attachments.Count.ToString());

        if (response.Usage is { } usage)
        {
            table.AddRow("Input tokens", usage.InputTokenCount?.ToString("N0") ?? "-");
            table.AddRow("Output tokens", usage.OutputTokenCount?.ToString("N0") ?? "-");
            table.AddRow("Total tokens", usage.TotalTokenCount?.ToString("N0") ?? "-");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static string? ReadPromptWithTokenCount()
    {
        var input = new StringBuilder();
        var cursorPosition = 0;
        var historyIndex = s_promptHistory.Count;
        var previousLineLength = 0;
        RenderPromptLine(input, 0, cursorPosition, ref previousLineLength);

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
                    AnsiConsole.Write($"\r{new string(' ', previousLineLength)}\r");
                    return null;
                case ConsoleKey.Backspace when cursorPosition > 0:
                    input.Remove(cursorPosition - 1, 1);
                    cursorPosition--;
                    break;
                case ConsoleKey.Delete when cursorPosition < input.Length:
                    input.Remove(cursorPosition, 1);
                    break;
                case ConsoleKey.LeftArrow when cursorPosition > 0:
                    cursorPosition = (key.Modifiers & ConsoleModifiers.Control) != 0
                        ? FindPreviousWordBoundary(input, cursorPosition)
                        : cursorPosition - 1;
                    break;
                case ConsoleKey.RightArrow when cursorPosition < input.Length:
                    cursorPosition = (key.Modifiers & ConsoleModifiers.Control) != 0
                        ? FindNextWordBoundary(input, cursorPosition)
                        : cursorPosition + 1;
                    break;
                case ConsoleKey.Home:
                    cursorPosition = 0;
                    break;
                case ConsoleKey.End:
                    cursorPosition = input.Length;
                    break;
                case ConsoleKey.UpArrow when historyIndex > 0:
                    historyIndex--;
                    input.Clear().Append(s_promptHistory[historyIndex]);
                    cursorPosition = input.Length;
                    break;
                case ConsoleKey.DownArrow:
                    if (historyIndex < s_promptHistory.Count - 1)
                    {
                        historyIndex++;
                        input.Clear().Append(s_promptHistory[historyIndex]);
                        cursorPosition = input.Length;
                    }
                    else if (historyIndex < s_promptHistory.Count)
                    {
                        historyIndex = s_promptHistory.Count;
                        input.Clear();
                        cursorPosition = 0;
                    }
                    break;
                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        input.Insert(cursorPosition, key.KeyChar);
                        cursorPosition++;
                    }
                    break;
            }

            var tokens = input.Length > 0 ? s_tokenizer.CountTokens(input.ToString()) : 0;
            RenderPromptLine(input, tokens, cursorPosition, ref previousLineLength);
        }
    }

    private static void RenderPromptLine(
        StringBuilder input,
        int tokenCount,
        int cursorPosition,
        ref int previousLineLength)
    {
        var tokenPart = $"[~{tokenCount}]";
        var currentLineLength = tokenPart.Length + 3 + input.Length;
        var padding = currentLineLength < previousLineLength
            ? new string(' ', previousLineLength - currentLineLength)
            : string.Empty;

        System.Console.Write($"\r\x1b[90m{tokenPart}\x1b[0m \x1b[34m>\x1b[0m {input}{padding}");

        var moveBack = input.Length + padding.Length - cursorPosition;
        if (moveBack > 0)
            System.Console.Write($"\x1b[{moveBack}D");

        previousLineLength = currentLineLength;
    }

    private static int FindPreviousWordBoundary(StringBuilder input, int cursorPosition)
    {
        var position = cursorPosition - 1;
        while (position > 0 && char.IsWhiteSpace(input[position]))
            position--;
        while (position > 0 && !char.IsWhiteSpace(input[position - 1]))
            position--;
        return position;
    }

    private static int FindNextWordBoundary(StringBuilder input, int cursorPosition)
    {
        var position = cursorPosition;
        while (position < input.Length && !char.IsWhiteSpace(input[position]))
            position++;
        while (position < input.Length && char.IsWhiteSpace(input[position]))
            position++;
        return position;
    }
}
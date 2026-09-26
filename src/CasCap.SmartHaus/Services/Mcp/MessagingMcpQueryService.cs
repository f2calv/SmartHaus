namespace CasCap.Services;

/// <summary>
/// Exposes messaging poll operations as MCP tools for the Comms Agent, through the Signalizr
/// gateway.
/// </summary>
/// <remarks>
/// Bakes in the chat channel so the agent only needs to provide the poll question and answer
/// options. The gateway owns the account and resolves the channel to its Signal group.
/// </remarks>
[McpServerToolType]
public sealed partial class MessagingMcpQueryService(
    ISignalizrClient signalizrClient,
    IPollTracker pollTracker,
    string channelName)
{
    /// <summary>
    /// Creates a poll in the configured chat channel.
    /// </summary>
    [McpServerTool]
    [Description("Sends a multiple-choice question to the user's messaging group. ALWAYS use this tool when you would list options, choices, suggestions, recommendations, or alternatives — even if the user does not say 'poll'. Trigger phrases include 'give me options', 'what are my choices', 'suggest some', 'which should I', or any request that results in a numbered/bulleted list of possibilities. After calling this tool, do NOT send a follow-up text message — the poll itself is the response.")]
    public async Task<PollCreatedResult> CreatePoll(
        [Description("Short question for the poll, e.g. 'Which room lights should I turn off?'")] string question,
        [Description("Comma-separated answer options, 2–8 choices, e.g. 'Yes, No, Maybe'.")] string answers,
        CancellationToken cancellationToken = default)
    {
        var answerArray = answers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var pollId = await signalizrClient.CreatePollAsync(channelName, question, answerArray,
            cancellationToken: cancellationToken);
        pollTracker.TrackPoll(pollId, question, answerArray, channelName);

        return new PollCreatedResult { PollId = pollId };
    }

    /// <summary>
    /// Closes an existing poll in the chat channel.
    /// </summary>
    [McpServerTool]
    [Description("Closes a previously created poll. Use the identifier from the create response.")]
    public async Task<bool> ClosePoll(
        [Description("The poll identifier returned when the poll was created.")] string pollId,
        CancellationToken cancellationToken = default)
    {
        await signalizrClient.ClosePollAsync(channelName, pollId, cancellationToken);
        pollTracker.RemovePoll(pollId);
        return true;
    }

    /// <summary>
    /// Returns the current vote tally for a tracked poll.
    /// </summary>
    [McpServerTool]
    [Description("Gets the current vote status of a previously created poll. Returns the question, options, vote counts, and a summary. Returns null if no poll with the given identifier is tracked.")]
    public Task<PollStatusResult?> GetPollStatus(
        [Description("The poll identifier returned when the poll was created.")] string pollId,
        CancellationToken cancellationToken = default)
    {
        var poll = pollTracker.GetPoll(pollId);
        if (poll is null)
            return Task.FromResult<PollStatusResult?>(null);

        return Task.FromResult<PollStatusResult?>(new PollStatusResult
        {
            PollId = poll.PollId,
            Question = poll.Question,
            Answers = poll.Answers,
            TotalVotes = poll.Votes.Count,
            Summary = poll.BuildResultSummary(),
            IsActedUpon = poll.IsActedUpon,
        });
    }
}

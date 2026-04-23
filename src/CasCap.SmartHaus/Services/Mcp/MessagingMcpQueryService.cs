namespace CasCap.Services;

/// <summary>
/// MCP wrapper for <see cref="SignalCliRestClientService"/> that exposes messaging
/// poll operations as MCP tools for the Comms Agent.
/// </summary>
/// <remarks>
/// <para>
/// Bakes in the account phone number and dynamically resolves the target group ID from
/// the configured group name so the agent only needs to provide the poll question and
/// answer options.
/// </para>
/// <para>
/// Temporary: these tools call <see cref="SignalCliRestClientService"/> directly until
/// poll operations are added to <see cref="CasCap.Common.Abstractions.INotifier"/>.
/// </para>
/// </remarks>
[McpServerToolType]
public partial class MessagingMcpQueryService(
    SignalCliRestClientService signalCliSvc,
    IPollTracker pollTracker,
    string phoneNumber,
    string groupName)
{
    /// <summary>
    /// Creates a poll in the configured notification group.
    /// </summary>
    [McpServerTool]
    [Description("Sends a multiple-choice question to the user's messaging group. ALWAYS use this tool when you would list options, choices, suggestions, recommendations, or alternatives — even if the user does not say 'poll'. Trigger phrases include 'give me options', 'what are my choices', 'suggest some', 'which should I', or any request that results in a numbered/bulleted list of possibilities. After calling this tool, do NOT send a follow-up text message — the poll itself is the response.")]
    public async Task<CreatePollResponse?> CreatePoll(
        [Description("Short question for the poll, e.g. 'Which room lights should I turn off?'")] string question,
        [Description("Comma-separated answer options, 2–8 choices, e.g. 'Yes, No, Maybe'.")] string answers,
        CancellationToken cancellationToken = default)
    {
        var groupId = await ResolveGroupIdAsync();
        if (groupId is null)
            return null;

        var answerArray = answers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var request = new CreatePollRequest
        {
            Question = question,
            Answers = answerArray,
            Recipient = groupId,
        };
        var response = await signalCliSvc.CreatePoll(phoneNumber, request);
        if (response is not null)
            pollTracker.TrackPoll(response.Timestamp, question, answerArray, groupId);

        return response;
    }

    /// <summary>
    /// Closes an existing poll in the notification group.
    /// </summary>
    [McpServerTool]
    [Description("Closes a previously created poll. Use the identifier from the create response.")]
    public async Task<bool> ClosePoll(
        [Description("The poll identifier returned when the poll was created.")] string pollId,
        CancellationToken cancellationToken = default)
    {
        var groupId = await ResolveGroupIdAsync();
        if (groupId is null)
            return false;

        var result = await signalCliSvc.ClosePoll(phoneNumber, new ClosePollRequest
        {
            PollTimestamp = pollId,
            Recipient = groupId,
        });
        if (result)
            pollTracker.RemovePoll(pollId);

        return result;
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

    #region private helpers

    private async Task<string?> ResolveGroupIdAsync() =>
        (await signalCliSvc.ListGroups(phoneNumber))
            ?.FirstOrDefault(g => g.Name == groupName)?.Id;

    #endregion
}

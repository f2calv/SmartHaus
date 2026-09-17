using System.Collections.Concurrent;

namespace CasCap.Tests.Unit;

/// <summary>In-memory <see cref="IPollTracker"/> substitute; the tests never exercise poll votes.</summary>
public sealed class FakePollTracker : IPollTracker
{
    private readonly ConcurrentDictionary<string, ActivePoll> _polls = new();

    /// <inheritdoc/>
    public void TrackPoll(string pollId, string question, string[] answers, string groupId) =>
        _polls[pollId] = new ActivePoll
        {
            PollId = pollId,
            Question = question,
            Answers = answers,
            GroupId = groupId,
            CreatedUtc = DateTime.UtcNow,
        };

    /// <inheritdoc/>
    public bool RecordVote(string pollId, string voter, int[] selectedIndices)
    {
        if (!_polls.TryGetValue(pollId, out var poll))
            return false;
        poll.Votes[voter] = selectedIndices;
        return true;
    }

    /// <inheritdoc/>
    public ActivePoll? GetPoll(string pollId) => _polls.TryGetValue(pollId, out var poll) ? poll : null;

    /// <inheritdoc/>
    public bool RemovePoll(string pollId) => _polls.TryRemove(pollId, out _);

    /// <inheritdoc/>
    public IReadOnlyList<ActivePoll> GetActivePolls() => [.. _polls.Values];
}

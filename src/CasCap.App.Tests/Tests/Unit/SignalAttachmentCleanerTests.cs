using Microsoft.Extensions.Logging.Abstractions;

namespace CasCap.Tests.Unit;

/// <summary>
/// Retry, classification and completeness tests for <see cref="SignalAttachmentCleaner"/>.
/// </summary>
/// <remarks>
/// Retry delays are overridden to zero so the schedule itself is asserted once, rather than paid
/// for in wall-clock time by every exhaustion test.
/// </remarks>
[Trait("Category", "Messaging")]
public class SignalAttachmentCleanerTests
{
    [Fact]
    public void DefaultRetryPolicy_MatchesSpecification()
    {
        Assert.Equal(
            [TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4)],
            SignalAttachmentCleaner.DefaultRetryDelays);
        //Three delays give four total attempts.
        Assert.Equal(4, SignalAttachmentCleaner.DefaultRetryDelays.Length + 1);
        Assert.Equal(TimeSpan.FromSeconds(10), SignalAttachmentCleaner.DefaultAttemptTimeout);
    }

    [Fact]
    public async Task DeleteAll_SucceedsOnFirstAttempt()
    {
        var client = new FakeSignalCliClient { Stored = { "a1" } };
        var cleaner = CreateCleaner(client);

        var result = await cleaner.DeleteAllAsync(["a1"], TestContext.Current.CancellationToken);

        Assert.True(result.Complete);
        Assert.Equal(["a1"], result.Deleted);
        Assert.Empty(result.Remaining);
        Assert.Single(client.DeleteCalls);
        Assert.Equal(0, client.ListCallCount);
    }

    [Fact]
    public async Task DeleteAll_AlreadyAbsentIsTerminalSuccess()
    {
        //The client collapses 404 to false, so absence from the store is what proves success.
        var client = new FakeSignalCliClient { DeleteResponder = (_, _) => false };
        var cleaner = CreateCleaner(client);

        var result = await cleaner.DeleteAllAsync(["gone"], TestContext.Current.CancellationToken);

        Assert.True(result.Complete);
        Assert.Equal(["gone"], result.Deleted);
        Assert.Single(client.DeleteCalls);
        Assert.Equal(1, client.ListCallCount);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task DeleteAll_RetriesTransientFailureUntilSuccess(int succeedOnAttempt)
    {
        var client = new FakeSignalCliClient { Stored = { "a1" } };
        client.DeleteResponder = (_, attempt) => attempt >= succeedOnAttempt;
        var cleaner = CreateCleaner(client);

        var result = await cleaner.DeleteAllAsync(["a1"], TestContext.Current.CancellationToken);

        Assert.True(result.Complete);
        Assert.Equal(succeedOnAttempt, client.AttemptsFor("a1"));
    }

    [Fact]
    public async Task DeleteAll_ExhaustsFourAttemptsThenReportsRemaining()
    {
        var client = new FakeSignalCliClient { Stored = { "stuck" }, DeleteResponder = (_, _) => false };
        var cleaner = CreateCleaner(client);

        var result = await cleaner.DeleteAllAsync(["stuck"], TestContext.Current.CancellationToken);

        Assert.False(result.Complete);
        Assert.Equal(["stuck"], result.Remaining);
        Assert.Empty(result.Deleted);
        Assert.Equal(4, client.AttemptsFor("stuck"));
    }

    [Fact]
    public async Task DeleteAll_TerminalClientFailureExhaustsBudgetAndReportsRemaining()
    {
        //A 400 or authorization rejection surfaces as an exception from the transport layer.
        var client = new FakeSignalCliClient
        {
            Stored = { "denied" },
            DeleteResponder = (_, _) => throw new HttpRequestException("forbidden"),
        };
        var cleaner = CreateCleaner(client);

        var result = await cleaner.DeleteAllAsync(["denied"], TestContext.Current.CancellationToken);

        Assert.False(result.Complete);
        Assert.Equal(["denied"], result.Remaining);
        Assert.Equal(4, client.AttemptsFor("denied"));
    }

    [Fact]
    public async Task DeleteAll_DeletesEveryAttachmentIncludingUnselected()
    {
        var client = new FakeSignalCliClient { Stored = { "a1", "a2", "a3" } };
        var cleaner = CreateCleaner(client);

        var result = await cleaner.DeleteAllAsync(["a1", "a2", "a3"], TestContext.Current.CancellationToken);

        Assert.True(result.Complete);
        Assert.Equal(["a1", "a2", "a3"], result.Deleted);
        Assert.Empty(client.Stored);
    }

    [Fact]
    public async Task DeleteAll_PartialFailureReportsBothSides()
    {
        var client = new FakeSignalCliClient { Stored = { "ok1", "bad", "ok2" } };
        client.DeleteResponder = (id, _) => id != "bad";
        var cleaner = CreateCleaner(client);

        var result = await cleaner.DeleteAllAsync(["ok1", "bad", "ok2"], TestContext.Current.CancellationToken);

        Assert.False(result.Complete);
        Assert.Equal(["ok1", "ok2"], result.Deleted);
        Assert.Equal(["bad"], result.Remaining);
    }

    [Fact]
    public async Task DeleteAll_SkipsBlankIdentifiers()
    {
        var client = new FakeSignalCliClient { Stored = { "a1" } };
        var cleaner = CreateCleaner(client);

        var result = await cleaner.DeleteAllAsync(["", "  ", "a1"], TestContext.Current.CancellationToken);

        Assert.True(result.Complete);
        Assert.Equal(["a1"], result.Deleted);
        Assert.Single(client.DeleteCalls);
    }

    [Fact]
    public async Task DeleteAll_CancellationStopsAndReportsRemaining()
    {
        var client = new FakeSignalCliClient { Stored = { "a1" } };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var cleaner = CreateCleaner(client);

        var result = await cleaner.DeleteAllAsync(["a1"], cts.Token);

        Assert.False(result.Complete);
        Assert.Equal(["a1"], result.Remaining);
        Assert.Empty(client.DeleteCalls);
    }

    [Fact]
    public async Task DeleteAll_EmptyInputIsComplete()
    {
        var client = new FakeSignalCliClient();
        var cleaner = CreateCleaner(client);

        var result = await cleaner.DeleteAllAsync([], TestContext.Current.CancellationToken);

        Assert.True(result.Complete);
        Assert.Empty(result.Deleted);
        Assert.Empty(client.DeleteCalls);
    }

    private static SignalAttachmentCleaner CreateCleaner(FakeSignalCliClient client) =>
        new(NullLogger<SignalAttachmentCleaner>.Instance, TimeProvider.System, client,
            retryDelays: [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero]);
}

namespace CasCap.Services;

/// <summary>
/// Deletes inbound Signal attachments through <see cref="ISignalCliClient"/>, retrying bounded
/// transient failures before reporting an attachment as remaining.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ISignalCliClient.DeleteAttachment"/> collapses the HTTP response to a
/// <see cref="bool"/>, so a failed delete cannot be classified by status code here. A failure is
/// therefore resolved by listing the attachment store: an identifier that is no longer present is
/// treated as terminal success (the <c>404</c> case), and anything else is retried until the
/// attempt budget is exhausted. Terminal client-side failures such as <c>400</c> or an
/// authorization rejection cost the full budget rather than failing fast, but reach the same
/// outcome.
/// </para>
/// <para>
/// Cancellation stops further attempts and reports the outstanding identifiers rather than
/// throwing, because cleanup runs from a <see langword="finally"/> block whose caller must still
/// observe a deterministic outcome.
/// </para>
/// </remarks>
public sealed class SignalAttachmentCleaner : ISignalAttachmentCleaner
{
    /// <summary>Delays applied between successive delete attempts for one attachment.</summary>
    /// <remarks>Three delays give four total attempts.</remarks>
    public static readonly TimeSpan[] DefaultRetryDelays =
        [TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4)];

    /// <summary>The time budget for a single delete attempt.</summary>
    public static readonly TimeSpan DefaultAttemptTimeout = TimeSpan.FromSeconds(10);

    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ISignalCliClient _signalCliClient;
    private readonly TimeSpan[] _retryDelays;
    private readonly TimeSpan _attemptTimeout;

    /// <summary>Initializes a new instance of the <see cref="SignalAttachmentCleaner"/> class.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="timeProvider">Clock used for the inter-attempt delays.</param>
    /// <param name="signalCliClient">The signal-cli REST surface.</param>
    /// <param name="retryDelays">Overrides <see cref="DefaultRetryDelays"/>; supplied by tests only.</param>
    /// <param name="attemptTimeout">Overrides <see cref="DefaultAttemptTimeout"/>; supplied by tests only.</param>
    public SignalAttachmentCleaner(
        ILogger<SignalAttachmentCleaner> logger,
        TimeProvider timeProvider,
        ISignalCliClient signalCliClient,
        TimeSpan[]? retryDelays = null,
        TimeSpan? attemptTimeout = null)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _signalCliClient = signalCliClient;
        _retryDelays = retryDelays ?? DefaultRetryDelays;
        _attemptTimeout = attemptTimeout ?? DefaultAttemptTimeout;
    }

    /// <inheritdoc/>
    public async Task<AttachmentCleanupResult> DeleteAllAsync(IReadOnlyList<string> attachmentIds,
        CancellationToken cancellationToken = default)
    {
        var deleted = new List<string>(attachmentIds.Count);
        var remaining = new List<string>();

        foreach (var id in attachmentIds)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;
            if (await DeleteOneAsync(id, cancellationToken))
                deleted.Add(id);
            else
                remaining.Add(id);
        }

        if (remaining.Count > 0)
            _logger.LogError("{ClassName} cleanup incomplete, deleted={DeletedCount}, remaining={RemainingCount}, remainingIds={RemainingIds}",
                nameof(SignalAttachmentCleaner), deleted.Count, remaining.Count, string.Join(", ", remaining));
        else if (deleted.Count > 0)
            _logger.LogDebug("{ClassName} deleted {DeletedCount} attachment(s)", nameof(SignalAttachmentCleaner), deleted.Count);

        return new AttachmentCleanupResult { Deleted = deleted, Remaining = remaining };
    }

    private async Task<bool> DeleteOneAsync(string attachmentId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= _retryDelays.Length; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptCts.CancelAfter(_attemptTimeout);
            try
            {
                if (await _signalCliClient.DeleteAttachment(attachmentId, attemptCts.Token))
                    return true;

                //The client reports any non-success as false, so an already-absent attachment is
                //indistinguishable from a retryable failure without consulting the store.
                if (await IsAbsentAsync(attachmentId, attemptCts.Token))
                    return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{ClassName} delete attempt {Attempt} failed for {AttachmentId} ({ExceptionType})",
                    nameof(SignalAttachmentCleaner), attempt + 1, attachmentId, ex.GetType().Name);
            }

            if (attempt == _retryDelays.Length)
                break;

            try
            {
                await Task.Delay(_retryDelays[attempt], _timeProvider, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
        return false;
    }

    private async Task<bool> IsAbsentAsync(string attachmentId, CancellationToken cancellationToken)
    {
        var stored = await _signalCliClient.ListAttachments(cancellationToken);
        return stored is not null && !stored.Contains(attachmentId, StringComparer.Ordinal);
    }
}

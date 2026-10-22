using System.Net;

namespace CasCap.Tests.Infrastructure;

/// <summary>A recording provider stub; no request leaves the in-process handler.</summary>
public sealed class MieleHttpHandler : HttpMessageHandler
{
    /// <summary>Gets captured request paths, JSON bodies and cancellation tokens.</summary>
    public List<(string Path, string? Body, CancellationToken Token)> Calls { get; } = [];

    /// <summary>Signals when the current request reaches the provider stub.</summary>
    public TaskCompletionSource Started { get; private set; } = NewSignal();

    /// <summary>Signals when a blocked provider request is cancelled.</summary>
    public TaskCompletionSource Cancelled { get; private set; } = NewSignal();

    /// <summary>Gets whether the provider response content was disposed.</summary>
    public bool ResponseDisposed { get; private set; }

    private HttpStatusCode _status = HttpStatusCode.OK;
    private string _json = "{}";
    private bool _block;
    private Exception? _failure;

    /// <summary>Configures a deterministic response and clears previous requests/signals.</summary>
    public void Reset(HttpStatusCode status = HttpStatusCode.OK, string json = "{}", bool block = false, Exception? failure = null)
    {
        _status = status;
        _json = json;
        _block = block;
        _failure = failure;
        Calls.Clear();
        Started = NewSignal();
        Cancelled = NewSignal();
        ResponseDisposed = false;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Calls.Add((request.RequestUri!.PathAndQuery, body, cancellationToken));
        Started.TrySetResult();
        if (_failure is { } failure)
            throw failure;
        if (_block)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Cancelled.TrySetResult();
                throw;
            }
        }
        return new HttpResponseMessage(_status)
        {
            Content = new ResponseContent(_json, () => ResponseDisposed = true),
        };
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class ResponseContent(string json, Action disposed) : StringContent(json, System.Text.Encoding.UTF8, "application/json")
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                disposed();
            base.Dispose(disposing);
        }
    }
}

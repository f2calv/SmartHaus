using System.Net;
using System.Text;

namespace CasCap.Tests.Unit;

/// <summary>
/// Recorded details of a single outbound request, captured before the underlying
/// <see cref="HttpRequestMessage"/> is disposed by <c>HttpClientBase</c>.
/// </summary>
/// <param name="Method">The HTTP method used.</param>
/// <param name="Uri">The fully-resolved request URI.</param>
/// <param name="Body">The serialized request body, or <see langword="null"/> when there was none.</param>
/// <param name="Authorization">The <c>Authorization</c> header value, or <see langword="null"/> when unset.</param>
public sealed record RecordedCall(HttpMethod Method, Uri Uri, string? Body, string? Authorization)
{
    /// <summary>The request path and query, without scheme or authority.</summary>
    public string PathAndQuery => Uri.PathAndQuery;
}

/// <summary>
/// An <see cref="HttpMessageHandler"/> that records every request and replies from a caller-supplied
/// responder, so the signal-cli client can be exercised without a live server or any credentials.
/// </summary>
/// <param name="responder">Produces the response for a given request.</param>
public sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    /// <summary>Every request observed, in order.</summary>
    public List<RecordedCall> Calls { get; } = [];

    /// <summary>Creates a handler that replies to every request with the same JSON payload.</summary>
    public static StubHttpMessageHandler RespondJson(string json, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

    /// <summary>Creates a handler that replies to every request with the given status and no body.</summary>
    public static StubHttpMessageHandler RespondStatus(HttpStatusCode statusCode) =>
        new(_ => new HttpResponseMessage(statusCode) { Content = new StringContent(string.Empty) });

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Calls.Add(new RecordedCall(request.Method, request.RequestUri!, body, request.Headers.Authorization?.ToString()));

        return responder(request);
    }
}

/// <summary>
/// Returns a single pre-built <see cref="HttpClient"/> regardless of the requested name.
/// </summary>
/// <param name="client">The client to hand out.</param>
public sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    /// <inheritdoc/>
    public HttpClient CreateClient(string name) => client;
}

using CasCap.Common.Services;

namespace CasCap.Services;

/// <summary>HTTP client for discovering the external IP address.</summary>
public sealed class DDnsFindMyIpClientService : HttpClientBase
{
    /// <summary>Initializes a new instance of the <see cref="DDnsFindMyIpClientService"/> class.</summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    public DDnsFindMyIpClientService(ILogger<DDnsFindMyIpClientService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        Client = httpClientFactory.CreateClient(nameof(DDnsFindMyIpClientService));
    }

    /// <summary>Gets the current external IP address.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The external IP address, or null if discovery fails.</returns>
    public async Task<IPAddress?> GetIp(CancellationToken cancellationToken)
    {
        IPAddress? res = null;
        var requestUri = $"?format=json";
        try
        {
            var tpl = await base.GetAsync<FindMyIpResponse, object>(requestUri, cancellationToken: cancellationToken);
            if (tpl.result is not null && tpl.result.Ip is not null)
            {
                if (IPAddress.TryParse(tpl.result.Ip, out res))
                    _logger.LogDebug("{ClassName} current IP address resolved", nameof(DDnsFindMyIpClientService));
                else
                    _logger.LogDebug("{ClassName} unable to parse current IP address", nameof(DDnsFindMyIpClientService));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // The caller reports the failure transition; keep the exception detail at Debug.
            _logger.LogDebug(ex, "{ClassName} IP address lookup failed", nameof(DDnsFindMyIpClientService));
        }
        return res;
    }
}

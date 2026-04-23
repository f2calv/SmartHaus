using CasCap.Common.Services;

namespace CasCap.Services;

/// <summary>HTTP client for discovering the external IP address.</summary>
public class DDnsFindMyIpClientService : HttpClientBase
{
    /// <summary>Initializes a new instance of the <see cref="DDnsFindMyIpClientService"/> class.</summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="config">Dynamic DNS configuration.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="env">Host environment.</param>
    public DDnsFindMyIpClientService(ILogger<DDnsFindMyIpClientService> logger,
        IOptions<DDnsConfig> config,
        IHttpClientFactory httpClientFactory,
        IHostEnvironment env)
    {
        _logger = logger;
        Client = httpClientFactory.CreateClient(nameof(DDnsFindMyIpClientService));
        if (env.IsDevelopment() && config.Value.JsonDebugEnabled && !string.IsNullOrWhiteSpace(config.Value.JsonDebugPath))
            JsonDebugPath = config.Value.JsonDebugPath;
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
                    _logger.LogDebug("{ClassName} current IP address is {Ip}", nameof(DDnsFindMyIpClientService), res);
                else
                    _logger.LogWarning("{ClassName} unable to determine current IP address", nameof(DDnsFindMyIpClientService));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} something is broken..", nameof(DDnsFindMyIpClientService));
        }
        return res;
    }
}

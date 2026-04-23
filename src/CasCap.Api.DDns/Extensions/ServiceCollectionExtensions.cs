namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Extension methods for configuring Dynamic DNS services.</summary>
public static class DDnsServiceCollectionExtensions
{

    /// <summary>
    /// Registers Dynamic DNS services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers only the HTTP client and its minimal
    /// dependencies without the <see cref="IBgFeature"/> background service.
    /// </param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    public static void AddDDns(this IServiceCollection services, IConfiguration configuration, bool lite = false,
        Action<DDnsConfig>? configure = null)
    {
        services.AddAndGetCasCapConfiguration<DDnsConfig>(configuration, configure);

        services.AddHttpClient(nameof(DDnsFindMyIpClientService), (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<DDnsConfig>>().Value;
            client.BaseAddress = new Uri(opts.BaseAddress);
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
        .AddStandardResilience(nameof(DDnsFindMyIpClientService));

        services.AddSingleton<DDnsFindMyIpClientService>();

        services.AddSingleton<DDnsQueryService>();
        services.AddSingleton<IDDnsQueryService>(sp => sp.GetRequiredService<DDnsQueryService>());

        if (!lite)
            services.AddSingleton<IBgFeature, DDnsBgService>();
    }
}

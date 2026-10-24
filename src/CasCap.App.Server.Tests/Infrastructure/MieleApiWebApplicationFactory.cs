using CasCap.Controllers;
using CasCap.Extensions;
using CasCap.HealthChecks;
using Microsoft.AspNetCore.Hosting;

namespace CasCap.Tests.Infrastructure;

/// <summary>Runs the real Miele client/query/controller stack against a fake provider.</summary>
public sealed class MieleApiWebApplicationFactory : CasCapAppWebApplicationFactory
{
    /// <summary>Gets the fake provider used by the named HTTP client.</summary>
    public MieleHttpHandler Provider { get; } = new();

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        var values = new Dictionary<string, string?>
        {
            ["CasCap:MieleConfig:OAuthToken"] = "example",
            ["CasCap:MieleConfig:HealthCheckUri"] = "https://example.com/v1/",
            ["CasCap:MieleConfig:EventStreamUrl"] = "https://example.com/events",
            ["CasCap:MieleConfig:Sinks:AvailableSinks:Memory:Enabled"] = "false",
        };
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
        builder.ConfigureServices(services =>
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            services.AddMiele(configuration);
            services.AddHttpClient(nameof(MieleConnectionHealthCheck))
                .ConfigurePrimaryHttpMessageHandler(() => Provider);
            services.AddControllers().AddApplicationPart(typeof(MieleController).Assembly);
        });
    }
}

using Microsoft.AspNetCore.Hosting;

namespace CasCap.Tests.Infrastructure;

/// <summary>Adds synthetic contract probes to the credential-free application host.</summary>
public sealed class ApiContractWebApplicationFactory : CasCapAppWebApplicationFactory
{
    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
            services.AddControllers().AddApplicationPart(typeof(ApiContractProbeController).Assembly));
    }
}

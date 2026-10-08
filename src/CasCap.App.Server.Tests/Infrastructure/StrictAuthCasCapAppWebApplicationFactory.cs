using CasCap.Common.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;

namespace CasCap.Tests.Infrastructure;

/// <summary>Hosts the application with its production Basic authentication policy enabled.</summary>
internal sealed class StrictAuthCasCapAppWebApplicationFactory : CasCapAppWebApplicationFactory
{
    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.AddAuthorizationBuilder()
                .SetDefaultPolicy(
                    new AuthorizationPolicyBuilder()
                        .AddAuthenticationSchemes(BasicAuthenticationHandler.SchemeName)
                        .RequireAuthenticatedUser()
                        .Build());
        });
    }
}
using CasCap.Common.Extensions;
using CasCap.Models;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace CasCap.Tests;

/// <summary>Builds the configuration shared by the device integration test projects.</summary>
/// <remarks>
/// Linked into each device test project so the standard Test configuration chain and the optional
/// Key Vault source are composed in one place.
/// </remarks>
// TODO: Move this into CasCap.Api.Azure.Auth as a public configuration extension the next time that
// package is released, so other consumers can share it; kept local to avoid an extra release in the
// 1.2.0 release train.
internal static class TestConfiguration
{
    /// <summary>Adds the Test configuration chain and Key Vault, when the AppConfig enables it.</summary>
    internal static ConfigurationManager AddDeviceTestConfiguration(this ConfigurationManager configuration, Assembly assembly)
    {
        configuration.AddStandardConfiguration("Test", assembly)
            .AddKeyVaultConfigurationFrom(c =>
            {
                var authConfig = c.GetSection(AzureAuthConfig.ConfigurationSectionName).Get<AzureAuthConfig>();
                return (authConfig?.KeyVaultUri, authConfig?.TokenCredential);
            });
        return configuration;
    }
}

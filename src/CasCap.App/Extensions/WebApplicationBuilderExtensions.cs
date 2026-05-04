using CasCap.Abstractions;

namespace CasCap.Extensions;

/// <summary>
/// Shared configuration bootstrapping for any <see cref="IHostApplicationBuilder"/>
/// (e.g. <see cref="WebApplicationBuilder"/>, <see cref="HostApplicationBuilder"/>).
/// </summary>
public static class HostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds standard configuration sources, Key Vault secrets and binds all strongly-typed
    /// option sections used across the solution.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="assembly">
    /// Assembly used for user-secrets loading. Pass <c>typeof(Program).Assembly</c> from the entry point.
    /// </param>
    public static (AppConfig appConfig, AIConfig aiConfig, ApiAuthConfig apiAuthConfig, HashSet<string> enabledFeatures, GitMetadata gitMetadata) InitializeConfiguration(
        this IHostApplicationBuilder builder, Assembly assembly)
    {
        builder.Configuration.AddStandardConfiguration(builder.Environment.EnvironmentName, assembly);

        //bind AppConfig for the first time
        var appConfig = builder.Configuration.GetSection(AppConfig.ConfigurationSectionName).Get<AppConfig>()
            ?? throw new GenericException($"{nameof(AppConfig)} cannot be null!");
        if (appConfig.IsKeyVaultEnabled)
            builder.Configuration.AddKeyVaultConfiguration(appConfig.KeyVaultUri, appConfig.TokenCredential);
        builder.Services.AddCasCapConfiguration<AppConfig>();
        builder.Services.AddCasCapConfiguration<AzureAuthConfig>();

        //bind AppConfig for the second time as now we have additional configuration/secrets loaded from Key Vault
        appConfig = builder.Configuration.GetSection(AppConfig.ConfigurationSectionName).Get<AppConfig>()
            ?? throw new GenericException($"{nameof(AppConfig)} cannot be null!");
        builder.Services.AddSingleton<IKubeAppConfig>(appConfig);
        builder.Services.AddSingleton<IAzureAuthConfig>(appConfig);
        var gitMetadata = new GitMetadata();
        builder.Services.AddSingleton(gitMetadata);

        var featureConfig = builder.Services.AddAndGetCasCapConfiguration<FeatureConfig>(builder.Configuration);
        var enabledFeatures = featureConfig.GetEnabledFeatures();
        var aiConfig = builder.Services.AddAndGetCasCapConfiguration<AIConfig>(builder.Configuration);
        var apiAuthConfig = builder.Services.AddAndGetCasCapConfiguration<ApiAuthConfig>(builder.Configuration);

        // Always bind SignalRHubConfig — hub-client pods read HubPath from this section;
        // falls back to built-in defaults if the section is absent from appsettings.json.
        builder.Services.AddCasCapConfiguration<SignalRHubConfig>();

        return (appConfig, aiConfig, apiAuthConfig, enabledFeatures, gitMetadata);
    }
}

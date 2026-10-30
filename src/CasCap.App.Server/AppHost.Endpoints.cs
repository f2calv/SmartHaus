using CasCap.Common.Authentication;
using Microsoft.AspNetCore.Authentication;
using Serilog;

namespace CasCap;

public static partial class AppHost
{
    private static void MapEndpoints(
        WebApplication app,
        AppConfig appConfig,
        IReadOnlySet<string> enabledFeatures,
        SignalRHubConfig? signalRHubConfig)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        if (!app.Environment.IsDevelopment())
        {
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments($"/{appConfig.SwaggerUriRoutePrefix}")
                    && context.User.Identity?.IsAuthenticated != true)
                {
                    await context.ChallengeAsync(BasicAuthenticationHandler.SchemeName);
                    return;
                }
                await next();
            });
        }

        app.UseSwagger(options =>
        {
            options.RouteTemplate = $"{appConfig.SwaggerUriRoutePrefix}/{{documentName}}.json";
        });
        app.UseSwaggerUI(options =>
        {
            foreach (var endpoint in appConfig.SwaggerEndpoints.Where(pair => pair.Value is not null))
                options.SwaggerEndpoint(endpoint.Value, endpoint.Key);
            options.RoutePrefix = appConfig.SwaggerUriRoutePrefix;
        });

        if (app.Environment.IsDevelopment())
        {
            app.MapGet("/debug/routes", (IEnumerable<EndpointDataSource> endpointSources) =>
                string.Join("\n", endpointSources.SelectMany(source => source.Endpoints)));
        }

        MapMcp(app, appConfig);
        app.MapControllers();
        app.MapRazorPages();
        MapSignalR(app, enabledFeatures, signalRHubConfig);

        app.MapHealthChecks("/healthz", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        }).AllowAnonymous();

        foreach (var probeType in Enum.GetValues<KubernetesProbeTypes>())
        {
            if (probeType is KubernetesProbeTypes.None)
                continue;
            var tag = probeType.GetDescription();
            app.MapHealthChecks($"/healthz/{tag}", new HealthCheckOptions
            {
                Predicate = healthCheck => healthCheck.Tags.Contains(tag),
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            }).AllowAnonymous();
        }

        if (app.Environment.IsDevelopment() && appConfig.OtlpExporterEndpoint is not null)
            app.UseOpenTelemetryPrometheusScrapingEndpoint();

        app.UseCasCapRequestLogging();
    }
}

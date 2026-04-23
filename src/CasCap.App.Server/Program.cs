using CasCap.Authentication;
using Microsoft.AspNetCore.Authentication;
using Serilog;
using StackExchange.Redis;

MonitoringExtensions.GetBootstrapLogger();

var result = 0;
try
{
    var builder = WebApplication.CreateBuilder(args);
    var (appConfig, connectionStrings, aiConfig, _, enabledFeatures, gitMetadata) = builder.InitializeConfiguration(typeof(Program).Assembly);
    var logger = builder.InitializeSerilog();
    var connectionMultiplexer = builder.Services.AddCasCapCaching(builder.Configuration)
        ?? throw new GenericException($"Failed to create {nameof(IConnectionMultiplexer)}");
    builder.InitializeOpenTelemetry(appConfig, connectionStrings, connectionMultiplexer, gitMetadata);

    if (enabledFeatures.Count == 0)
        throw new GenericException($"{nameof(enabledFeatures)} is not set via Configuration (i.e. appsettings.json or ENV variable)");
    else
        logger.LogInformation("{ClassName} {AppName} running on {NodeName} with features {@Flags}",
            nameof(Program), appConfig.PodName ?? AppDomain.CurrentDomain.FriendlyName, appConfig.NodeName ?? Environment.MachineName, enabledFeatures);

    #region standard services + feature flags

    var mcpBuilder = builder.Services.AddMcpServer()
        .WithHttpTransport(options => options.IdleTimeout = Timeout.InfiniteTimeSpan)
        //.WithToolsFromAssembly()
        ;

    // SystemMcpQueryService is referenced by all agents — register unconditionally.
    builder.Services.AddSystemMcp();

    // Register SignalR services unconditionally so IHubContext<> is always resolvable for
    // HausHub sinks discovered during assembly scanning (e.g. HausHubSinkBuderusService).
    // The hub endpoint mapping and Redis backplane are configured later when SignalRHub is enabled.
    builder.Services.AddSignalR();

    if (enabledFeatures.Contains(FeatureNames.Buderus) || enabledFeatures.Contains(FeatureNames.Comms))
    {
        builder.Services.AddBuderusWithExtraSinks(builder.Configuration,
            lite: !enabledFeatures.Contains(FeatureNames.Buderus),
            additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
        builder.Services.AddHeatPumpMcp();
        mcpBuilder.WithToolsFromAssembly(typeof(HeatPumpMcpQueryService).Assembly);
        mcpBuilder.WithPromptsFromAssembly(typeof(HeatPumpMcpQueryService).Assembly);
    }

    if (enabledFeatures.Contains(FeatureNames.Sicce))
        builder.Services.AddSicceWithExtraSinks(builder.Configuration,
            additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);

    if (enabledFeatures.Contains(FeatureNames.Wiz) || enabledFeatures.Contains(FeatureNames.Comms))
    {
        builder.Services.AddWizWithExtraSinks(builder.Configuration,
            lite: !enabledFeatures.Contains(FeatureNames.Wiz),
            additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
    }

    if (enabledFeatures.Contains(FeatureNames.EdgeHardware))
        builder.Services.AddEdgeHardwarePi(builder.Configuration);

    if (enabledFeatures.Contains(FeatureNames.EdgeHardware) || enabledFeatures.Contains(FeatureNames.Comms))
    {
        builder.Services.AddEdgeHardwareWithExtraSinks(builder.Configuration,
            lite: !enabledFeatures.Contains(FeatureNames.EdgeHardware),
            cpuEnabled: enabledFeatures.Contains(FeatureNames.EdgeHardware),
            additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
        builder.Services.AddEdgeHardwareMcp();
        mcpBuilder.WithToolsFromAssembly(typeof(EdgeHardwareMcpQueryService).Assembly);
        mcpBuilder.WithPromptsFromAssembly(typeof(EdgeHardwareMcpQueryService).Assembly);
    }

    if (enabledFeatures.Contains(FeatureNames.DoorBird) || enabledFeatures.Contains(FeatureNames.Comms))
    {
        builder.Services.AddMediaStreamSink();
        builder.Services.AddDoorBirdWithExtraSinks(builder.Configuration,
            lite: !enabledFeatures.Contains(FeatureNames.DoorBird),
            tokenCredential: appConfig.TokenCredential,
            additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
        builder.Services.AddFrontDoorMcp();
        mcpBuilder.WithToolsFromAssembly(typeof(FrontDoorMcpQueryService).Assembly);
        mcpBuilder.WithPromptsFromAssembly(typeof(FrontDoorMcpQueryService).Assembly);
    }

    if (enabledFeatures.Contains(FeatureNames.DDns))
        builder.Services.AddDDns(builder.Configuration);

    if (enabledFeatures.Contains(FeatureNames.Knx) || enabledFeatures.Contains(FeatureNames.Comms))
    {
        builder.Services.AddKnxWithExtraSinks(builder.Configuration,
            lite: !enabledFeatures.Contains(FeatureNames.Knx),
            additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
        builder.Services.AddBusSystemMcp();
        mcpBuilder.WithToolsFromAssembly(typeof(BusSystemMcpQueryService).Assembly);
        mcpBuilder.WithPromptsFromAssembly(typeof(BusSystemMcpQueryService).Assembly);
    }

    if (enabledFeatures.Contains(FeatureNames.Fronius) || enabledFeatures.Contains(FeatureNames.Comms))
    {
        builder.Services.AddFroniusWithExtraSinks(builder.Configuration,
            lite: !enabledFeatures.Contains(FeatureNames.Fronius),
            additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
        builder.Services.AddInverterMcp();
        mcpBuilder.WithToolsFromAssembly(typeof(InverterMcpQueryService).Assembly);
        mcpBuilder.WithPromptsFromAssembly(typeof(InverterMcpQueryService).Assembly);
    }

    if (enabledFeatures.Contains(FeatureNames.Shelly) || enabledFeatures.Contains(FeatureNames.Comms))
    {
        builder.Services.AddShellyWithExtraSinks(builder.Configuration,
            lite: !enabledFeatures.Contains(FeatureNames.Shelly),
            additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
        builder.Services.AddSmartPlugMcp();
        mcpBuilder.WithToolsFromAssembly(typeof(SmartPlugMcpQueryService).Assembly);
        mcpBuilder.WithPromptsFromAssembly(typeof(SmartPlugMcpQueryService).Assembly);
    }

    // SmartLightingMcp spans Wiz, KNX and Shelly — all three DI parameters are nullable
    if (enabledFeatures.Contains(FeatureNames.Wiz) || enabledFeatures.Contains(FeatureNames.Knx)
        || enabledFeatures.Contains(FeatureNames.Shelly) || enabledFeatures.Contains(FeatureNames.Comms))
    {
        builder.Services.AddSmartLightingMcp();
        mcpBuilder.WithToolsFromAssembly(typeof(SmartLightingMcpQueryService).Assembly);
        mcpBuilder.WithPromptsFromAssembly(typeof(SmartLightingMcpQueryService).Assembly);
    }

    if (enabledFeatures.Contains(FeatureNames.Ubiquiti))
    {
        builder.Services.AddUbiquitiWithExtraSinks(builder.Configuration,
            lite: !enabledFeatures.Contains(FeatureNames.Ubiquiti),
            additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
        builder.Services.AddCamerasMcp();
        mcpBuilder.WithToolsFromAssembly(typeof(IpCameraMcpQueryService).Assembly);
        mcpBuilder.WithPromptsFromAssembly(typeof(IpCameraMcpQueryService).Assembly);
    }

    if (enabledFeatures.Contains(FeatureNames.Miele))
    {
        builder.Services.AddMieleWithExtraSinks(builder.Configuration,
            lite: !enabledFeatures.Contains(FeatureNames.Miele),
            additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
        builder.Services.AddAppliancesMcp();
        mcpBuilder.WithToolsFromAssembly(typeof(AppliancesMcpQueryService).Assembly);
        mcpBuilder.WithPromptsFromAssembly(typeof(AppliancesMcpQueryService).Assembly);
    }

    if (enabledFeatures.Contains(FeatureNames.Comms))
        builder.AddComms();

    // Register all AI agent profiles with deferred tool resolution
    var otelSourceName = AgentExtensions.GetAISourceName(appConfig.MetricNamePrefix);
    foreach (var (agentName, agentConfig) in aiConfig.Agents.Where(a => a.Value.Enabled))
    {
        var provider = aiConfig.Providers[agentConfig.Provider];
        builder.Services.AddKeyedSingleton(agentName, (sp, _) =>
        {
            // Resolves tools declared in AgentConfig.Tools[] with include/exclude filtering.
            // Uses deferred resolution to avoid circular singleton dependencies
            // (e.g. DoorBirdQueryService → SecurityAgentSinkCommsStreamService → AIAgent → DoorBirdQueryService).
            var tools = AgentExtensions.CreateToolsForAgent(sp, agentConfig, aiConfig,
                deferResolution: true, isDevelopment: builder.Environment.IsDevelopment(),
                instructionsAssembly: typeof(HausServiceCollectionExtensions).Assembly);

            var (_, agent, _) = builder.CreateAgent(provider, agentConfig, sp, tools, otelSourceName, aiConfig: aiConfig);
            return agent;
        });
    }

    builder.Services.AddSingleton<IEventSink<CommsEvent>, CommsStreamSinkService>();

    builder.Services.AddFeatureFlagService(enabledFeatures);
    SignalRHubConfig? signalRHubConfig = null;
    if (enabledFeatures.Contains(FeatureNames.SignalRHub))
    {
        signalRHubConfig = builder.Services.AddSignalRHub(builder.Configuration);
        builder.Services.AddSignalR()
            .AddStackExchangeRedis(connectionMultiplexer.Configuration, options =>
            {
                options.Configuration.ChannelPrefix = RedisChannel.Literal(FeatureNames.SignalRHub);
            });
    }
    #endregion

    #region HealthChecks
    _ = builder.Services.AddHealthChecks();
    //if (builder.Environment.IsDevelopment())
    //    builder.Services.AddHealthChecksUI(options =>
    //    {
    //        options.AddHealthCheckEndpoint(AppDomain.CurrentDomain.FriendlyName, builder.Environment.IsDevelopment() ? "http://localhost:8080/healthz-ui" : "healthz");
    //        options.SetEvaluationTimeInSeconds(5);
    //        options.SetMinimumSecondsBetweenFailureNotifications(10);
    //    })
    //    .AddInMemoryStorage();
    #endregion

    #region WebAPI & route config
    builder.Services.AddAuthentication(BasicAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(BasicAuthenticationHandler.SchemeName, null);

    if (builder.Environment.IsDevelopment())
        builder.Services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build());

    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1.0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    var mvcBuilder = builder.Services.AddControllers()
        .ConfigureApplicationPartManager(manager =>
        {
            manager.ApplicationParts.Clear();
            //Note: SystemController is always registered
            manager.ApplicationParts.Add(new Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart(typeof(SystemController).Assembly));
        })
        //.AddControllersAsServices()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        })
        ;

    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var apiLogger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("ApiModelValidation");

            var errors = context.ModelState
                .Where(state => state.Value?.Errors.Count > 0)
                .ToDictionary(
                    state => state.Key,
                    state => state.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

            apiLogger.LogWarning(
                "{ClassName} invalid model state for {Method} {Path} with QueryString={QueryString} ContentType={ContentType} ContentLength={ContentLength} Errors={Errors}",
                nameof(Program),
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path,
                context.HttpContext.Request.QueryString.ToString(),
                context.HttpContext.Request.ContentType,
                context.HttpContext.Request.ContentLength,
                errors);

            return new BadRequestObjectResult(context.ModelState);
        };
    });

    if (enabledFeatures.Contains(FeatureNames.Buderus))
        mvcBuilder.AddApplicationPart(typeof(BuderusController).Assembly);
    if (enabledFeatures.Contains(FeatureNames.DoorBird))
        mvcBuilder.AddApplicationPart(typeof(DoorBirdController).Assembly);
    if (enabledFeatures.Contains(FeatureNames.Fronius))
        mvcBuilder.AddApplicationPart(typeof(FroniusController).Assembly);
    if (enabledFeatures.Contains(FeatureNames.Wiz))
        mvcBuilder.AddApplicationPart(typeof(WizController).Assembly);
    if (enabledFeatures.Contains(FeatureNames.Knx))
        mvcBuilder.AddApplicationPart(typeof(BusController).Assembly);
    if (enabledFeatures.Contains(FeatureNames.Shelly))
        mvcBuilder.AddApplicationPart(typeof(ShellyController).Assembly);
    if (enabledFeatures.Contains(FeatureNames.Sicce))
        mvcBuilder.AddApplicationPart(typeof(SicceController).Assembly);
    if (enabledFeatures.Contains(FeatureNames.EdgeHardware))
        mvcBuilder.AddApplicationPart(typeof(EdgeHardwareController).Assembly);
    if (enabledFeatures.Contains(FeatureNames.Miele))
        mvcBuilder.AddApplicationPart(typeof(MieleController).Assembly);
    if (enabledFeatures.Contains(FeatureNames.Ubiquiti))
        mvcBuilder.AddApplicationPart(typeof(UbiquitiController).Assembly);

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Version = "v1",
            Title = "Haus API",
            Description = "does what it says on the tin"
        });

        options.AddSecurityDefinition(BasicAuthenticationHandler.SchemeName, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "basic",
            Description = "Enter your username and password"
        });

        options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecuritySchemeReference(BasicAuthenticationHandler.SchemeName, doc),
                []
            }
        });

        var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly);
        foreach (var xmlFile in xmlFiles)
            options.IncludeXmlComments(xmlFile, includeControllerXmlComments: true);

        options.AddOperationFilterInstance(new InheritDocOperationFilter());
    });

    builder.Services.AddRazorPages().AddRazorPagesOptions(options =>
    {
        //options.RootDirectory = "/Content";
    });
    builder.Services.Configure<RouteOptions>(options =>
    {
        options.LowercaseUrls = true;
        //options.LowercaseQueryStrings = true;
        options.AppendTrailingSlash = false;
        //options.ContraintMap.Add("Custom", typeof(CustomConstraint));
    });
    //builder.WebHost.UseWebRoot("wwwroot");
    //builder.WebHost.UseStaticWebAssets();
    #endregion

    var app = builder.Build();

    logger.LogInformation("{ClassName} starting", nameof(Program));

    #region route mapping
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    if (!app.Environment.IsDevelopment())
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments($"/{appConfig.SwaggerUriRoutePrefix}") && context.User.Identity?.IsAuthenticated != true)
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
        foreach (var endpoint in appConfig.SwaggerEndpoints.Where(p => p.Value is not null))
            options.SwaggerEndpoint(endpoint.Value, endpoint.Key);
        options.RoutePrefix = appConfig.SwaggerUriRoutePrefix;
    });
    if (app.Environment.IsDevelopment())
    {
        //https://www.meziantou.net/list-all-routes-in-an-asp-net-core-application.htm
        app.MapGet("/debug/routes", (IEnumerable<EndpointDataSource> endpointSources) =>
            string.Join("\n", endpointSources.SelectMany(source => source.Endpoints)));
    }
    app.MapMcp(aiConfig.McpUrl);
    app.MapControllers();
    app.MapRazorPages();

    if (enabledFeatures.Contains(FeatureNames.SignalRHub))
        app.MapHub<HausHub>(signalRHubConfig!.HubPath).RequireAuthorization();
    //app.MapGet("/", async context => { await context.Response.WriteAsync($"Hello World!"); });

    app.MapHealthChecks("/healthz", new HealthCheckOptions { ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse }).AllowAnonymous();
    foreach (var probeType in Enum.GetValues<KubernetesProbeTypes>())
    {
        if (probeType is KubernetesProbeTypes.None)
            continue;
        var tag = probeType.GetDescription();
        app.MapHealthChecks($"/healthz/{tag}", new HealthCheckOptions
        {
            Predicate = (healthCheck) => healthCheck.Tags.Contains(tag),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        }).AllowAnonymous();
    }

    //app.MapMetrics();
    if (app.Environment.IsDevelopment()
        && connectionStrings.OtlpExporter is not null && connectionStrings.OtlpExporter != default)
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
    //app.UseOpenTelemetryPrometheusScrapingEndpoint(context => context.Request.Path == "/metrics"
    //    && context.Connection.LocalPort == networkOptions.metrics_healthcheck);
    #endregion

    //Log.Logger = app.Logger;
    app.Services.AddStaticLogging();

    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = (httpContext, elapsed, ex) =>
        {
            if (ex is null
                && httpContext.Response.StatusCode == 200
                && httpContext.Request.Path.StartsWithSegments("/healthz"))
                return Serilog.Events.LogEventLevel.Verbose;

            return Serilog.Events.LogEventLevel.Information;
        };
    });

    await app.RunAsync();
}
catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
{
    result = 1;
    Log.Fatal(ex, "{AppName} terminated unexpectedly", AppDomain.CurrentDomain.FriendlyName);
}
catch (Exception ex1)
{
    result = 1;
    Log.Fatal(ex1, "Unhandled exception");
}
finally
{
    Log.Information("Stopped {AppName}", AppDomain.CurrentDomain.FriendlyName);
    await Log.CloseAndFlushAsync();
}
return result;

/// <summary>Program class for integration testing.</summary>
public partial class Program { }

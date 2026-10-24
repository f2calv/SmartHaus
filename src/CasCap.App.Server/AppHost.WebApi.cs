using CasCap.Common.Authentication;
using CasCap.Constants;
using Microsoft.AspNetCore.Authentication;

namespace CasCap;

public static partial class AppHost
{
    private static void AddWebApi(
        WebApplicationBuilder builder,
        IReadOnlySet<string> enabledFeatures)
    {
        builder.Services.AddHealthChecks();
        builder.Services.AddAuthentication(BasicAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(BasicAuthenticationHandler.SchemeName, null);

        if (builder.Environment.IsDevelopment())
            builder.Services.AddAuthorizationBuilder()
                .SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build());

        builder.Services.AddApiVersioning(options =>
        {
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddMvc()
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        var mvcBuilder = builder.Services.AddControllers()
            .ConfigureApplicationPartManager(manager =>
            {
                manager.ApplicationParts.Clear();
                // SystemController is always registered.
                manager.ApplicationParts.Add(
                    new Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart(typeof(SystemController).Assembly));
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        if (enabledFeatures.Contains(FeatureNames.Buderus))
            mvcBuilder.AddApplicationPart(typeof(BuderusController).Assembly);
        if (enabledFeatures.Contains(FeatureNames.DDns))
            mvcBuilder.AddApplicationPart(typeof(DDnsController).Assembly);
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

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference(BasicAuthenticationHandler.SchemeName, document),
                    []
                }
            });

            var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly);
            foreach (var xmlFile in xmlFiles)
                options.IncludeXmlComments(xmlFile, includeControllerXmlComments: true);

            options.AddOperationFilterInstance(new InheritDocOperationFilter());
        });

        builder.Services.AddRazorPages().AddRazorPagesOptions(_ => { });
        builder.Services.Configure<RouteOptions>(options =>
        {
            options.LowercaseUrls = true;
            options.AppendTrailingSlash = false;
        });
        builder.Services.AddProblemDetails();
    }
}

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .MinimumLevel.Override("CasCap", LogEventLevel.Information)
    .WriteTo.Console(outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// Set the static LoggerFactory early so classes with static ILogger fields
// (e.g. SinkServiceCollectionExtensions) can resolve loggers during service registration.
ApplicationLogging.LoggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(Log.Logger);

try
{
    // ── Configuration + DI ────────────────────────────────────────────────────
    var builder = Host.CreateApplicationBuilder();
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger);
    var (_, _, _, enabledFeatures, _) = builder.InitializeConfiguration(typeof(Program).Assembly);

    if (enabledFeatures.Count == 0)
        throw new InvalidOperationException($"{nameof(enabledFeatures)} is empty — set CasCap:FeatureConfig:EnabledFeatures in appsettings or environment variables.");

    builder.Services.AddCasCapCaching(builder.Configuration);

    // SystemMcpQueryService is referenced by all agents — register unconditionally.
    builder.Services.AddSystemMcp();

    if (enabledFeatures.Contains(FeatureNames.Buderus))
    {
        builder.Services.AddBuderusWithExtraSinks(builder.Configuration, lite: true);
        builder.Services.AddHeatPumpMcp();
    }

    if (enabledFeatures.Contains(FeatureNames.Wiz))
        builder.Services.AddWizWithExtraSinks(builder.Configuration, lite: true);

    if (enabledFeatures.Contains(FeatureNames.EdgeHardware))
    {
        builder.Services.AddEdgeHardwareWithExtraSinks(builder.Configuration, lite: true);
        builder.Services.AddEdgeHardwareMcp();
    }

    if (enabledFeatures.Contains(FeatureNames.DoorBird))
    {
        builder.Services.AddDoorBirdWithExtraSinks(builder.Configuration, lite: true);
        builder.Services.AddFrontDoorMcp();
    }

    if (enabledFeatures.Contains(FeatureNames.Knx))
    {
        builder.Services.AddKnxWithExtraSinks(builder.Configuration, lite: true);
        builder.Services.AddBusSystemMcp();
    }

    if (enabledFeatures.Contains(FeatureNames.Fronius))
    {
        builder.Services.AddFroniusWithExtraSinks(builder.Configuration, lite: true);
        builder.Services.AddInverterMcp();
    }

    if (enabledFeatures.Contains(FeatureNames.Shelly))
    {
        builder.Services.AddShellyWithExtraSinks(builder.Configuration, lite: true);
        builder.Services.AddSmartPlugMcp();
    }

    // SmartLightingMcp spans Wiz, KNX and Shelly — all three DI parameters are nullable
    if (enabledFeatures.Contains(FeatureNames.Wiz) || enabledFeatures.Contains(FeatureNames.Knx)
        || enabledFeatures.Contains(FeatureNames.Shelly))
        builder.Services.AddSmartLightingMcp();

    if (enabledFeatures.Contains(FeatureNames.Ubiquiti))
    {
        builder.Services.AddUbiquitiWithExtraSinks(builder.Configuration, lite: true);
        builder.Services.AddCamerasMcp();
    }

    if (enabledFeatures.Contains(FeatureNames.Miele))
    {
        builder.Services.AddMieleWithExtraSinks(builder.Configuration, lite: true);
        builder.Services.AddAppliancesMcp();
    }

    // Register a stub so agent configs referencing MessagingMcpQueryService pass DI validation
    // without requiring Signal CLI or Comms infrastructure.
    builder.Services.AddMessagingMcpStub();

    builder.Services.AddFeatureFlagService(enabledFeatures);

    builder.Services.AddSingleton<InMemorySessionStore>();
    builder.Services.AddSingleton<ISessionStore>(sp => sp.GetRequiredService<InMemorySessionStore>());
    builder.Services.AddSingleton<AgentCommandHandler>();
    builder.Services.AddSingleton<ConsoleApp>();

    using var host = builder.Build();

    // ── Run ───────────────────────────────────────────────────────────────────
    using var cts = new CancellationTokenSource();
    System.Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    host.Services.AddStaticLogging();

    var app = host.Services.GetRequiredService<ConsoleApp>();

    await app.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    // Normal cancellation via Ctrl+C
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;

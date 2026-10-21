using CasCap.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CasCap.Tests.Unit;

/// <summary>
/// Verifies that lite registration, used by the Comms pod, queries the live Redis sink rather than an
/// Azure Tables snapshot the writer pods never update.
/// </summary>
[Trait("Category", "Sinks")]
public sealed class LiteSinkRegistrationTests
{
    public static TheoryData<string, Type, Action<IServiceCollection, IConfiguration>> Registrations => new()
    {
        { nameof(BuderusConfig), typeof(IEventSink<BuderusEvent>), (s, c) => s.AddBuderusWithExtraSinks(c, lite: true) },
        { nameof(DoorBirdConfig), typeof(IEventSink<DoorBirdEvent>), (s, c) => s.AddDoorBirdWithExtraSinks(c, lite: true) },
        { nameof(EdgeHardwareConfig), typeof(IEventSink<EdgeHardwareEvent>), (s, c) => s.AddEdgeHardwareWithExtraSinks(c, lite: true) },
        { nameof(FroniusConfig), typeof(IEventSink<FroniusEvent>), (s, c) => s.AddFroniusWithExtraSinks(c, lite: true) },
        { nameof(MieleConfig), typeof(IEventSink<MieleEvent>), (s, c) => s.AddMieleWithExtraSinks(c, lite: true) },
        { nameof(ShellyConfig), typeof(IEventSink<ShellyEvent>), (s, c) => s.AddShellyWithExtraSinks(c, lite: true) },
        { nameof(SicceConfig), typeof(IEventSink<SicceEvent>), (s, c) => s.AddSicceWithExtraSinks(c, lite: true) },
        { nameof(UbiquitiConfig), typeof(IEventSink<UbiquitiEvent>), (s, c) => s.AddUbiquitiWithExtraSinks(c, lite: true) },
        { nameof(WizConfig), typeof(IEventSink<WizEvent>), (s, c) => s.AddWizWithExtraSinks(c, lite: true) },
    };

    [Theory]
    [MemberData(nameof(Registrations))]
    public void LiteRegistration_UsesRedisNotAzureTables(string configSection, Type sinkInterface,
        Action<IServiceCollection, IConfiguration> register)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"CasCap:{configSection}:Sinks:AvailableSinks:Redis:Enabled"] = "true",
                [$"CasCap:{configSection}:Sinks:AvailableSinks:AzureTables:Enabled"] = "true",
                //Synthetic values satisfying each config's required fields.
                [$"CasCap:{configSection}:BaseAddress"] = "http://device.example.com",
                [$"CasCap:{configSection}:AzureTableStorageConnectionString"] = "https://mystorageaccount.table.core.windows.net",
                [$"CasCap:{configSection}:AzureBlobStorageConnectionString"] = "https://mystorageaccount.blob.core.windows.net",
                [$"CasCap:{configSection}:AzureBlobStorageContainerName"] = "container",
                [$"CasCap:{configSection}:OAuthToken"] = "token",
                [$"CasCap:{configSection}:DeviceToken"] = "token",
                [$"CasCap:{configSection}:GatewayPassword"] = "password",
                [$"CasCap:{configSection}:Username"] = "user",
                [$"CasCap:{configSection}:Password"] = "password",
                [$"CasCap:{configSection}:AuthKey"] = "key",
                [$"CasCap:{configSection}:VendorKey"] = "key",
                [$"CasCap:{configSection}:PrivatePassword"] = "password",
                [$"CasCap:{configSection}:DoorControllerID"] = "controller",
                [$"CasCap:{configSection}:DoorControllerRelayID"] = "relay",
                [$"CasCap:{configSection}:DatapointMappings:/system:ColumnName"] = "system",
                [$"CasCap:{configSection}:Devices:0:DeviceId"] = "device",
                [$"CasCap:{configSection}:Devices:0:DeviceName"] = "Test Plug",
            })
            .Build();
        var services = new ServiceCollection();

        register(services, configuration);

        var keyedSinks = services
            .Where(sd => sd.ServiceType == sinkInterface && sd.IsKeyedService)
            .Select(sd => sd.ServiceKey as string)
            .ToList();
        Assert.Contains("Redis", keyedSinks);
        Assert.DoesNotContain("AzureTables", keyedSinks);
    }
}

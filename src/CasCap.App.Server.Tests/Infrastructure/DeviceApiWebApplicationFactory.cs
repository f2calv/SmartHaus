using CasCap.Abstractions;
using CasCap.Controllers;
using CasCap.Models.Dtos;
using Microsoft.AspNetCore.Hosting;

namespace CasCap.Tests.Infrastructure;

/// <summary>Exercises device controllers with recording services instead of device connections.</summary>
public sealed class DeviceApiWebApplicationFactory : CasCapAppWebApplicationFactory
{
    public List<CancellationToken> HistoryTokens { get; } = [];

    public List<bool> PowerCommands { get; } = [];

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.AddControllers()
                .AddApplicationPart(typeof(BuderusController).Assembly)
                .AddApplicationPart(typeof(FroniusController).Assembly)
                .AddApplicationPart(typeof(EdgeHardwareController).Assembly)
                .AddApplicationPart(typeof(SicceController).Assembly)
                .AddApplicationPart(typeof(ShellyController).Assembly)
                .AddApplicationPart(typeof(WizController).Assembly);
            services.AddSingleton<IBuderusQueryService>(new BuderusQueries(this));
            services.AddSingleton<IFroniusQueryService>(new FroniusQueries(this));
            services.AddSingleton<IEdgeHardwareQueryService>(new EdgeQueries(this));
            services.AddSingleton<ISicceQueryService>(new SicceQueries(this));
            services.AddSingleton<IShellyQueryService>(new ShellyQueries(this));
            services.AddSingleton<IWizQueryService>(new WizQueries(this));
        });
    }

    private IAsyncEnumerable<T> RecordHistory<T>(CancellationToken cancellationToken)
    {
        HistoryTokens.Add(cancellationToken);
        return Empty<T>();
    }

    private static async IAsyncEnumerable<T> Empty<T>()
    {
        await Task.CompletedTask;
        yield break;
    }

    private sealed class BuderusQueries(DeviceApiWebApplicationFactory factory) : IBuderusQueryService
    {
        public IAsyncEnumerable<BuderusEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
            => factory.RecordHistory<BuderusEvent>(cancellationToken);
        public Task<BuderusSnapshot> GetSnapshot() => throw new NotSupportedException();
        public Task<bool> SetDataPoint(string datapointId, string value, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FroniusQueries(DeviceApiWebApplicationFactory factory) : IFroniusQueryService
    {
        public IAsyncEnumerable<FroniusEvent> GetInverterReadings(int limit = 100, CancellationToken cancellationToken = default)
            => factory.RecordHistory<FroniusEvent>(cancellationToken);
        public Task<InverterSnapshot> GetInverterSnapshot() => throw new NotSupportedException();
        public Task<PowerFlowRealtimeData?> GetPowerFlowRealtimeData() => throw new NotSupportedException();
        public Task<CommonInverterData?> GetInverterRealtimeData(string dataCollection = "CommonInverterData") => throw new NotSupportedException();
        public Task<Dictionary<string, InverterInfoEntry>?> GetInverterInfo() => throw new NotSupportedException();
        public Task<ActiveDeviceInfoData?> GetActiveDeviceInfo() => throw new NotSupportedException();
        public Task<Dictionary<string, MeterRealtimeData>?> GetMeterRealtimeData(string scope = "System", int? deviceId = null) => throw new NotSupportedException();
        public Task<Dictionary<string, StorageRealtimeData>?> GetStorageRealtimeData(string scope = "System", int? deviceId = null) => throw new NotSupportedException();
    }

    private sealed class EdgeQueries(DeviceApiWebApplicationFactory factory) : IEdgeHardwareQueryService
    {
        public IAsyncEnumerable<EdgeHardwareEvent> GetEvents(int limit = 100, CancellationToken cancellationToken = default)
            => factory.RecordHistory<EdgeHardwareEvent>(cancellationToken);
        public Task<List<EdgeHardwareSnapshot>> GetLatestSnapshots() => throw new NotSupportedException();
    }

    private sealed class SicceQueries(DeviceApiWebApplicationFactory factory) : ISicceQueryService
    {
        public IAsyncEnumerable<SicceEvent> GetReadings(int limit = 100, CancellationToken cancellationToken = default)
            => factory.RecordHistory<SicceEvent>(cancellationToken);
        public Task<SicceSnapshot> GetSnapshot() => throw new NotSupportedException();
        public Task<DeviceInfo?> GetDeviceInfo() => throw new NotSupportedException();
    }

    private sealed class ShellyQueries(DeviceApiWebApplicationFactory factory) : IShellyQueryService
    {
        public IAsyncEnumerable<ShellyEvent> GetReadings(int limit = 100, CancellationToken cancellationToken = default)
            => factory.RecordHistory<ShellyEvent>(cancellationToken);
        public Task<List<ShellySnapshot>> GetSnapshots() => throw new NotSupportedException();
        public Task<ShellyDeviceStatusResponse?> GetDeviceStatus(string deviceId) => throw new NotSupportedException();
        public Task<ShellyRelayControlResponse?> SetRelayState(string deviceId, bool turnOn)
        {
            factory.PowerCommands.Add(turnOn);
            return Task.FromResult<ShellyRelayControlResponse?>(new ShellyRelayControlResponse { IsOk = true });
        }
    }

    private sealed class WizQueries(DeviceApiWebApplicationFactory factory) : IWizQueryService
    {
        public Task<bool> SetPowerState(string bulbIdentifier, bool on, CancellationToken cancellationToken = default)
        {
            factory.PowerCommands.Add(on);
            return Task.FromResult(true);
        }
        public IReadOnlyDictionary<string, WizBulb> GetDiscoveredBulbs() => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<string, WizBulb>> DiscoverBulbs(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WizPilotState?> GetPilot(string bulbIdentifier, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WizSystemConfig?> GetSystemConfig(string bulbIdentifier, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> SetPilot(string bulbIdentifier, WizSetPilotRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

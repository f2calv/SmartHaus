using Knx.Falcon;
using Microsoft.Extensions.Logging;
using Tiveria.Home.Knx.Cemi.Serializers;

namespace CasCap.Tests;

/// <summary>
/// Integration tests for the CEMI frame deserialization and <see cref="GroupValue"/> decoding pipeline.
/// Reads CEMI data from Azure Table Storage via <see cref="KnxSinkCemiAzTablesService"/>.
/// </summary>
public class CemiDecodingTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task DecodeCemiFrames_FromAzureTableStorage()
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<CemiDecodingTests>>();
        var cemiAzTablesSink = (KnxSinkCemiAzTablesService)_serviceProvider
            .GetRequiredKeyedService<IEventSink<KnxEvent>>("AzureTablesCemi");

        await _knxGroupAddressLookupSvc.GetLookup();

        var serializer = new CemiLDataSerializer();
        var cemiEntities = await cemiAzTablesSink.GetCemiReadingEntities(CancellationToken.None);

        Assert.NotEmpty(cemiEntities);
        _output.WriteLine($"Retrieved {cemiEntities.Count} CEMI entities from Azure Table Storage");

        var decoded = 0;
        foreach (var entity in cemiEntities)
        {
            var bytes = Convert.FromHexString(entity.CemiHex);
            if (!serializer.TryDeserialize(bytes, out var cemi) || cemi is null) continue;

            var groupAddress = ((Tiveria.Home.Knx.Primitives.GroupAddress)cemi.DestinationAddress).ToString();
            var kga = _knxGroupAddressLookupSvc.GetKGAByAddress(groupAddress);
            if (kga is null) continue;

            var dptBase = kga.GetDptBase();
            var isCompact = cemi.Apdu?.Data.Length == 1 && dptBase!.SizeInBit < 8;
            var groupValue = isCompact
                ? new GroupValue(cemi.Apdu!.Data[0], (int)dptBase!.SizeInBit)
                : new GroupValue(cemi.Apdu!.Data);
            var tpl = groupValue.DecodeValue(kga, logger);
            if (tpl.ValueDecoded is null) continue;

            var knxEvent = cemi.ToKnxEvent(entity.TimestampUtc, kga, tpl.ValueDecoded, tpl.ValueLabelDecoded);

            Assert.NotNull(knxEvent);
            Assert.NotNull(knxEvent.Args);
            Assert.NotNull(knxEvent.Kga);
            Assert.NotNull(knxEvent.Value);
            Assert.Equal(groupAddress, knxEvent.Kga.GroupAddress);

            var valueLabel = string.IsNullOrWhiteSpace(knxEvent.ValueLabel) ? string.Empty : $" ({knxEvent.ValueLabel})";
            _output.WriteLine($"telegram from '{knxEvent.Args.SourceAddress}' to '{knxEvent.Kga.GroupAddress}' " +
                $"({knxEvent.Kga.Name}, '{knxEvent.Value}{valueLabel}')");

            decoded++;
        }

        Assert.True(decoded > 0, "Expected at least one CEMI frame to decode successfully");
        _output.WriteLine($"Successfully decoded {decoded}/{cemiEntities.Count} CEMI frames");
    }
}

using Knx.Falcon;
using Tiveria.Home.Knx.Cemi;
using Tiveria.Home.Knx.Cemi.Serializers;
using Tiveria.Home.Knx.Primitives;
using CemiMessageCode = Tiveria.Home.Knx.Cemi.MessageCode;
using CemiPriority = Tiveria.Home.Knx.Cemi.Priority;

namespace CasCap.Extensions;

/// <summary>
/// Extension methods for converting KNX bus frames (<see cref="CemiLData"/>, <see cref="GroupEventArgs"/>)
/// into application-level DTOs and back.
/// </summary>
public static class CemiLDataExtensions
{
    private static readonly CemiLDataSerializer _serializer = new();

    /// <summary>
    /// Converts a <see cref="GroupEventArgs"/> received from the KNX bus into a <see cref="KnxGroupEvent"/>.
    /// </summary>
    /// <param name="args">The source <see cref="GroupEventArgs"/> received from the KNX bus.</param>
    public static KnxGroupEvent ToKnxGroupEvent(this GroupEventArgs args) => new()
    {
        EventType = args.EventType,
        MessagePriority = args.MessagePriority,
        HopCount = args.HopCount,
        Value = new KnxGroupValue
        {
            TypedValue = args.Value.TypedValue,
            Value = args.Value.Value,
            SizeInBit = args.Value.SizeInBit,
            IsShort = args.Value.IsShort,
        },
        SourceAddress = new KnxSourceAddress
        {
            AreaAddress = args.SourceAddress.AreaAddress,
            LineAddress = args.SourceAddress.LineAddress,
            DeviceAddress = args.SourceAddress.DeviceAddress,
            FullAddress = args.SourceAddress.FullAddress,
            SubnetAddress = args.SourceAddress.SubnetAddress,
            IndividualAddress = args.SourceAddress.ToString()
        },
        DestinationAddress = new KnxDestinationAddress
        {
            FullAddress = args.DestinationAddress.Address,
            GroupAddress = args.DestinationAddress.ToString()
        },
        IsSecure = args.IsSecure
    };

    /// <summary>
    /// Converts a <see cref="KnxGroupEvent"/> back into a <see cref="CemiLData"/> frame
    /// suitable for serialisation to raw CEMI bytes.
    /// </summary>
    /// <param name="kge">The application-level group event DTO.</param>
    /// <returns>A <see cref="CemiLData"/> frame representing the telegram.</returns>
    public static CemiLData ToCemiData(this KnxGroupEvent kge)
    {
        var srcAddress = Tiveria.Home.Knx.Primitives.IndividualAddress.Parse(kge.SourceAddress.IndividualAddress);
        var dstAddress = Tiveria.Home.Knx.Primitives.GroupAddress.Parse(kge.DestinationAddress.GroupAddress);

        var apduType = kge.EventType switch
        {
            GroupEventType.ValueWrite => ApduType.GroupValue_Write,
            GroupEventType.ValueResponse => ApduType.GroupValue_Response,
            GroupEventType.ValueRead => ApduType.GroupValue_Read,
            _ => ApduType.GroupValue_Write
        };

        var priority = kge.MessagePriority switch
        {
            MessagePriority.System => CemiPriority.System,
            MessagePriority.High => CemiPriority.Urgent,
            MessagePriority.Alarm => CemiPriority.Normal,
            _ => CemiPriority.Low
        };

        var apdu = new Apdu(apduType, kge.Value.Value);
        var cf1 = new ControlField1(extendedFrame: false, priority: priority,
            repeat: true, broadcast: BroadcastType.Normal, ack: false, confirm: ConfirmType.NoError);
        var cf2 = new ControlField2(groupAddress: true, hopCount: kge.HopCount, extendedFrameFormat: 0);
        var tpci = new Tpci(PacketType.Data, SequenceType.UnNumbered, 0, ControlType.None);

        return new CemiLData(CemiMessageCode.LDATA_IND, Array.Empty<AdditionalInformationField>(),
            srcAddress, dstAddress, cf1, cf2, tpci, apdu);
    }

    /// <summary>
    /// Serialises a <see cref="KnxGroupEvent"/> to a raw CEMI hex string via <see cref="ToCemiData"/>.
    /// </summary>
    /// <param name="kge">The application-level group event DTO.</param>
    /// <returns>The hex-encoded CEMI frame.</returns>
    public static string ToCemiHex(this KnxGroupEvent kge)
    {
        var cemi = kge.ToCemiData();
        var bytes = _serializer.Serialize(cemi);
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Converts a <see cref="CemiLData"/> frame into a <see cref="KnxEvent"/>.
    /// </summary>
    /// <param name="cemi">The deserialised CEMI frame.</param>
    /// <param name="timestamp">The UTC timestamp of the telegram.</param>
    /// <param name="kga">The parsed group address metadata.</param>
    /// <param name="valueDecoded">The decoded value produced by the datapoint decoder.</param>
    /// <param name="valueLabelDecoded">The human-readable label for the decoded value, or <see langword="null"/>.</param>
    public static KnxEvent ToKnxEvent(this CemiLData cemi, DateTime timestamp,
        KnxGroupAddressParsed kga, object valueDecoded, string? valueLabelDecoded)
    {
        var dptBase = kga.GetDptBase();
        var isCompact = cemi.Apdu?.Data.Length == 1 && dptBase!.SizeInBit < 8;

        var kge = new KnxGroupEvent
        {
            EventType = GroupEventType.ValueWrite,
            MessagePriority = MessagePriority.Low,
            HopCount = cemi.ControlField2.HopCount,
            Value = new KnxGroupValue
            {
                TypedValue = null!,
                SizeInBit = isCompact ? (int)dptBase!.SizeInBit : cemi.Apdu!.Data.Length * 8,
                IsShort = cemi.Apdu!.Data.Length <= 1,
                Value = cemi.Apdu!.Data
            },
            DestinationAddress = new KnxDestinationAddress
            {
                GroupAddress = ((Tiveria.Home.Knx.Primitives.GroupAddress)cemi.DestinationAddress).ToString(),
            },
            SourceAddress = new KnxSourceAddress
            {
                IndividualAddress = cemi.SourceAddress.ToString()
            },
            IsSecure = false
        };

        var groupValue = isCompact
            ? new GroupValue(cemi.Apdu!.Data[0], (int)dptBase!.SizeInBit)
            : new GroupValue(cemi.Apdu!.Data);

        return new KnxEvent(timestamp, kge, kga, groupValue, valueDecoded, valueLabelDecoded);
    }
}

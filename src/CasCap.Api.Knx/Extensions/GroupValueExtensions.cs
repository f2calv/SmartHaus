using Knx.Falcon;
using Tiveria.Home.Knx.Datapoint;

namespace CasCap.Extensions;

/// <summary>
/// Extension methods for decoding <see cref="GroupValue"/> payloads into friendly values
/// using both the Falcon and Tiveria datapoint libraries.
/// </summary>
public static class GroupValueExtensions
{
    /// <summary>
    /// Convert the encoded <see cref="GroupValue"/> into a usable/friendly value
    /// using both the Falcon and Tiveria datapoint libraries.
    /// </summary>
    /// <param name="groupValue">The raw <see cref="GroupValue"/> from the KNX bus.</param>
    /// <param name="kga">The parsed group address metadata containing DPT information.</param>
    /// <param name="logger">Logger for reporting decoding errors.</param>
    public static (object? ValueDecoded, string? ValueLabelDecoded) DecodeValue(
        this GroupValue groupValue, KnxGroupAddressParsed kga, ILogger logger)
    {
        //TODO: rework this entire function when we can pass a few hours of CEMI bus objects into it easily.
        var dptBase = kga.GetDptBase();

        //re-wrap non-compact GroupValues for compact DPTs — the serialization round-trip
        //through Redis streams loses the compact encoding (SizeInBit/IsShort metadata).
        if (!groupValue.IsShort && dptBase.SizeInBit < 8 && groupValue.Value.Length == 1)
            groupValue = new GroupValue(groupValue.Value[0], (int)dptBase.SizeInBit);

        //first decode the encoded value using the Falcon library function (hence the F letter on the end of the variable name)
        object valueDecodedF;
        try
        {
            valueDecodedF = dptBase.ToValue(groupValue);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{ClassName} raw value decoding failed for {GroupAddress}", nameof(GroupValueExtensions), kga.Name);
            return (null, null);
        }
        if (valueDecodedF is null)
            return (valueDecodedF, null);

        //get the data point type number, called a DPT id in the Tiveria library
        var dptId = dptBase.Number.ToString()!;//e.g. 1.003

        //exit early if the DPT id string comes up with weird outputs
        if (dptId == "3.007") //DPST-3-7, DPT_Control_Dimming
        {
            //some weird valueDecoded here! 'Control 1' or 'No control 0'
            return (valueDecodedF.ToString() == "Control 1", valueDecodedF.ToString() == "Control 1" ? "Increase" : "Decrease");
        }
        else if (dptId == "232.600") //DPST-232-600, DPT_Colour_RGB
        {
            //Tiveria returns RGB array, but a hex string is sufficient for now
            return (valueDecodedF, null);
        }
        else if (dptId == "10.001") //DPT_TimeOfDay, DPT_TimeOfDay
        {
            //Tiveria returns 'Tuesday, 18:38:05' vs 'Tuesday 18:38:05'
            return (valueDecodedF, null);
        }
        else if (dptId == "16.000")
        {
            //TODO: Tiveria handle later, DPT_String_ASCII
            return (valueDecodedF, null);
        }

        //search for the DPT in the Tiveria library
        var dpType = DatapointTypesList.GetTypeById(dptId);
        if (dpType is null)
        {
            if (dptId == "1.024")//substitute because Tiveria gets these values the wrong way around to the standard
                dpType = new DPType1("1.024", "Day/Night", "Night", "Day");
            else if (dptId == "9.029")//substitute
            {
                //TODO: Tiveria needs public ctr to mock
                //dpType = new DPType9("9.029", "Absolute Humidity", 0, 670760, "%");
                return (valueDecodedF, null);
            }
            else
            {
                logger.LogError("{ClassName} {DatapointTypesList} does not contain DPT id {DptId}",
                    nameof(GroupValueExtensions), nameof(DatapointTypesList), dptId);
                return (valueDecodedF, null);
            }
        }

        //then attempt to decode the value using the Tiveria library (hence the T letter on the end of the variable name)
        object? valueDecodedT = null;
        string? valueLabelDecodedT = null;//this is the string representation of the value, e.g. Day/Night, Door/Window, Open/Closed, etc...
        try
        {
            valueDecodedT = dpType.DecodeObject(groupValue.Value);//<--- Tiveria library function
            valueLabelDecodedT = dpType.DecodeString(groupValue.Value);//<--- Tiveria library function
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{ClassName} {GroupAddress}, DPTs={DPTs}", nameof(GroupValueExtensions), kga.GroupAddress, kga.DPTs);
        }
        if (valueDecodedT is null)
            return (valueDecodedF, null);

        //Tiveria makes *some* values cleaner, i.e. without the pointless float
        //precision so we then overwrite the value from Falcon library...but some values are not cleaner!
        if (dptId == "5.001") //DPT_Scaling i.e. percentage, 0 -> 100
        {
            if (valueDecodedT.ToString()!.Length < valueDecodedF.ToString()!.Length)
                valueDecodedF = valueDecodedT;
        }
        else if (dptId == "9.001" //DPT_Value_Temp i.e. temperature, 12.3
            || dptId == "9.007" //DPT_Value_Humidity i.e. 53.33
            )
        {
            if (valueDecodedT.ToString()!.Length < valueDecodedF.ToString()!.Length)
                valueDecodedF = valueDecodedT;
        }
        else if (valueDecodedT.ToString() != valueDecodedF.ToString())
        {
            if (valueLabelDecodedT == "Tiveria.Home.Knx.Datapoint.ComplexDateTime")
            {
                //Debugger.Break();
                return (valueDecodedF, null);
            }
            else if (valueDecodedT.ToString()!.StartsWith(valueDecodedF.ToString()!))
            {
                //we are happy with Tiveria rounding, 20.400000000000002 -> 20.4
                //Debugger.Break();
                valueDecodedF = valueDecodedT;
            }
            else
            {
                logger.LogError("{ClassName} {GroupAddress}, '{DPTs}' decoded value '{ValueDecodedF}' does not match Tiveria library decoded value '{ValueDecodedT}' ('{ValueLabelDecodedT}')",
                    nameof(GroupValueExtensions), kga.GroupAddress, kga.DPTs, valueDecodedF, valueDecodedT, valueLabelDecodedT);
                return (valueDecodedF, null);
            }
        }

        //if the string representation is the same as the value, then set it to null
        if (valueLabelDecodedT is not null && valueLabelDecodedT == valueDecodedT.ToString())
            valueLabelDecodedT = null;

        //do a final trim, maybe some values have weird whitespace in the Tiveria library, e.g. 'Day/Night ' instead of 'Day/Night'
        if (valueLabelDecodedT is not null)
            valueLabelDecodedT = valueLabelDecodedT.Trim();

        return (valueDecodedF, valueLabelDecodedT);
    }
}

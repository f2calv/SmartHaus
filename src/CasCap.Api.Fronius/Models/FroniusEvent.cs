namespace CasCap.Models;

/// <summary>Represents a Fronius inverter power flow reading event.</summary>
public record FroniusEvent
{
    /// <summary>Initializes a new instance from an API response.</summary>
    /// <param name="response">Power flow realtime data API response.</param>
    public FroniusEvent(ApiWrapper<PowerFlowRealtimeData> response)
    {
        TimestampUtc = response.Head!.Timestamp.ToUniversalTime();
        SOC = Math.Round(response.Body!.Data!.Inverters!.FirstOrDefault().Value.SOC / 100.0, 3);
        P_Akku = Math.Round(response.Body.Data.Site!.P_Akku, 1);
        P_Grid = Math.Round(response.Body.Data.Site.P_Grid, 1);
        P_Load = Math.Round(response.Body.Data.Site.P_Load, 1);
        P_PV = Math.Round(response.Body.Data.Site.P_PV, 1);
    }

    internal FroniusEvent(double soc, double pAkku, double pGrid, double pLoad, double pPv, DateTime timestampUtc)
    {
        SOC = soc;
        P_Akku = pAkku;
        P_Grid = pGrid;
        P_Load = pLoad;
        P_PV = pPv;
        TimestampUtc = timestampUtc;
    }

    /// <summary>UTC timestamp of the reading.</summary>
    public DateTime TimestampUtc { get; init; }

    /// <summary>
    /// SOC / State Of Charge
    /// </summary>
    [Description("Battery State Of Charge, a percentage value where 1 is 100%, 0.5 is 50%, 0.07 is 7%")]
    public double SOC { get; init; }

    /// <summary>
    /// P_Akku / Power from battery
    /// </summary>
    [Description("Power being drawn from the battery, units = Watts")]
    public double P_Akku { get; init; }

    /// <summary>
    /// P_Grid / Power From Grid
    /// </summary>
    [Description("Power being drawn from the grid, units = Watts")]
    public double P_Grid { get; init; }

    /// <summary>
    /// P_Load / Power Load
    /// </summary>
    [Description("Power load/consumption of the house, units = Watts")]
    public double P_Load { get; init; }

    /// <summary>
    /// P_PV / Photovoltaic Power
    /// </summary>
    [Description("Solar photovoltaic power being generated, units = Watts")]
    public double P_PV { get; init; }
}

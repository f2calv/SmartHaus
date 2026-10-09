using CasCap.Models.Dtos;

namespace CasCap.Tests;

/// <summary>
/// A synthetic house used as the ground truth of agent evaluation scenarios, rendered through the real
/// SmartHaus tool return types.
/// </summary>
/// <remarks>
/// Values are deliberately distinct so a deterministic grader cannot mistake a distractor for the answer:
/// five of twelve lights are on (one reports no state), and the only outdoor reading is 7.5 °C.
/// </remarks>
public static class SyntheticHouseTestData
{
    /// <summary>Timestamp of every synthetic reading.</summary>
    public static readonly DateTime ObservedUtc = new(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>Lights switched on.</summary>
    public const int LightsOn = 5;

    /// <summary>Lights in the house, including one without a reported state.</summary>
    public const int LightCount = 12;

    /// <summary>The heat pump's outdoor sensor reading in °C.</summary>
    public const double OutdoorTemperature = 7.5;

    /// <summary>The synthetic lights; <see cref="Light.IsOn"/> is <see langword="null"/> when no state has been reported.</summary>
    public static IReadOnlyList<Light> Lights { get; } =
    [
        new(FloorType.EG, RoomType.Kitchen, LightStyle.DL, CompassOrientation.North, true),
        new(FloorType.EG, RoomType.Kitchen, LightStyle.PL, CompassOrientation.South, false),
        new(FloorType.EG, RoomType.LivingRoom, LightStyle.DL, CompassOrientation.West, true),
        new(FloorType.EG, RoomType.Hallway, LightStyle.DL, CompassOrientation.East, false),
        new(FloorType.EG, RoomType.Entrance, LightStyle.DL, CompassOrientation.East, false),
        new(FloorType.EG, RoomType.GuestWC, LightStyle.DL, CompassOrientation.North, null),
        new(FloorType.OG, RoomType.MasterBedroom, LightStyle.DL, CompassOrientation.South, false),
        new(FloorType.OG, RoomType.ChildRoom, LightStyle.DL, CompassOrientation.West, true),
        new(FloorType.OG, RoomType.FamilyBathroom, LightStyle.DL, CompassOrientation.North, false),
        new(FloorType.DG, RoomType.Office, LightStyle.DL, CompassOrientation.South, true),
        new(FloorType.DG, RoomType.Office, LightStyle.WL, CompassOrientation.North, false),
        new(FloorType.DG, RoomType.Study, LightStyle.DL, CompassOrientation.East, true),
    ];

    /// <summary>Responses shared by every scenario: the clock and the building structure.</summary>
    public static IReadOnlyDictionary<string, Func<AIFunctionArguments, object?>> CommonResponses { get; } =
        new Dictionary<string, Func<AIFunctionArguments, object?>>
        {
            ["get_current_datetime_state"] = _ => new
            {
                LocalTime = ObservedUtc.AddHours(2),
                DayOfWeek = ObservedUtc.AddHours(2).DayOfWeek.ToString(),
                UtcOffset = "+02:00",
                TimeZone = "Europe/Berlin",
            },
            ["get_house_floors"] = _ => Lights.Select(l => l.Floor).Distinct().Select(f => new { Floor = f.ToString() }).ToList(),
            ["get_house_rooms"] = _ => Lights
                .GroupBy(l => l.Floor)
                .Select(g => new { Floor = g.Key.ToString(), Rooms = g.Select(l => l.Room.ToString()).Distinct().ToList() })
                .ToList(),
        };

    /// <summary>Responses for the lighting tools, honouring the floor, orientation and room filters.</summary>
    public static IReadOnlyDictionary<string, Func<AIFunctionArguments, object?>> LightingResponses { get; } =
        new Dictionary<string, Func<AIFunctionArguments, object?>>
        {
            ["get_house_light_states"] = args => Lights
                .Where(l => Matches(l.Floor, GetString(args, "floor")) && Matches(l.Orientation, GetString(args, "orientation")))
                .ToDictionary(l => $"{l.GroupName}-SW_FB", l => l.IsOn is { } isOn
                    ? new State($"{l.GroupName}-SW_FB", isOn ? "True" : "False", isOn ? nameof(DptSwitch.On) : nameof(DptSwitch.Off), ObservedUtc)
                    : null),
            ["get_house_smart_lights"] = args => Lights
                .Where(l => Matches(l.Room, GetString(args, "room")))
                .Select(ToKnxLight)
                .ToList(),
            ["get_house_light_state"] = args => Lights
                .Where(l => string.Equals(l.GroupName, GetString(args, "groupName"), StringComparison.OrdinalIgnoreCase))
                .Select(ToKnxLight)
                .FirstOrDefault(),
            ["get_smart_lights"] = _ => new Dictionary<string, object>(),
        };

    /// <summary>Responses for the heat pump and heating zone tools.</summary>
    public static IReadOnlyDictionary<string, Func<AIFunctionArguments, object?>> HeatingResponses { get; } =
        new Dictionary<string, Func<AIFunctionArguments, object?>>
        {
            ["get_heat_pump_state"] = _ => new BuderusSnapshot
            {
                Dhw1ActualTemp = 48.5,
                Dhw1SetTemperature = 50,
                Dhw1CurrentSetpoint = 50,
                Dhw1ExtraDhwStopTemp = 60,
                Hc1TemperatureException = 19,
                Hc1TemperatureNormal = 21,
                Hc1SupplyTemperatureSetpoint = 34.5,
                Hc2TemperatureException = 18,
                Hc2TemperatureNormal = 20,
                Hc2SupplyTemperatureSetpoint = 31,
                OutdoorTemperature = OutdoorTemperature,
            },
            ["get_house_heating_zones"] = _ => new List<KnxHvacZone>
            {
                new() { GroupName = "EG-HZ-Kitchen", Floor = FloorType.EG, Room = RoomType.Kitchen, Setpoint = 21, Temperature = 21.3, Humidity = 48 },
                new() { GroupName = "OG-HZ-MasterBedroom", Floor = FloorType.OG, Room = RoomType.MasterBedroom, Setpoint = 19, Temperature = 19.8, Humidity = 52 },
                new() { GroupName = "DG-HZ-Office", Floor = FloorType.DG, Room = RoomType.Office, Setpoint = 21, Temperature = 22.4, Humidity = 44 },
            },
            ["get_house_outdoor_temperature"] = _ => new
            {
                TemperatureCelsius = OutdoorTemperature,
                SensorLocation = "North wall",
                Source = "Heat pump outdoor sensor T1",
                TimestampUtc = ObservedUtc,
            },
        };

    /// <summary>Responses for door locks and the front door contact: the front door is closed but unlocked.</summary>
    public static IReadOnlyDictionary<string, Func<AIFunctionArguments, object?>> DoorResponses { get; } =
        new Dictionary<string, Func<AIFunctionArguments, object?>>
        {
            ["get_house_door_lock_states"] = _ => new KnxDoorLockSummary
            {
                LockCount = 2,
                LockedCount = 1,
                UnlockedCount = 1,
                Locks =
                [
                    new KnxDoorLock
                    {
                        GroupName = "EG-BI-Entrance(FrontDoor)-East-LOCK",
                        Floor = FloorType.EG,
                        Room = RoomType.Entrance,
                        Location = "FrontDoor",
                        Orientation = CompassOrientation.East,
                        State = DptLockState.Unlocked,
                    },
                    new KnxDoorLock
                    {
                        GroupName = "KG-BI-Garage(SideDoor)-West-LOCK",
                        Floor = FloorType.KG,
                        Room = RoomType.Garage,
                        Location = "SideDoor",
                        Orientation = CompassOrientation.West,
                        State = DptLockState.Locked,
                    },
                ],
            },
            ["get_house_front_door_state"] = _ => new FrontDoorContactState
            {
                IsOpen = false,
                State = "Closed",
                LastUpdated = ObservedUtc,
                Contact = new KnxContact
                {
                    GroupName = "EG-BI-Entrance(FrontDoor)-East-STATE",
                    Floor = FloorType.EG,
                    Location = "FrontDoor",
                    Orientation = CompassOrientation.East,
                    State = DptState.Inactive,
                },
            },
        };

    /// <summary>Merges response sets; later sets win when a tool name repeats.</summary>
    /// <param name="responseSets">The response sets to merge.</param>
    public static IReadOnlyDictionary<string, Func<AIFunctionArguments, object?>> Merge(
        params IReadOnlyDictionary<string, Func<AIFunctionArguments, object?>>[] responseSets)
    {
        var merged = new Dictionary<string, Func<AIFunctionArguments, object?>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, responder) in responseSets.SelectMany(s => s))
            merged[name] = responder;
        return merged;
    }

    /// <summary>Reads a string argument supplied by the model, which arrives as a <see cref="JsonElement"/> or a string.</summary>
    /// <param name="arguments">The model-supplied arguments.</param>
    /// <param name="name">The parameter name.</param>
    public static string? GetString(AIFunctionArguments arguments, string name) =>
        arguments.TryGetValue(name, out var value)
            ? value switch
            {
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
                JsonElement element => element.GetRawText(),
                null => null,
                _ => value.ToString(),
            }
            : null;

    private static bool Matches<TEnum>(TEnum value, string? filter) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(filter) || string.Equals(value.ToString(), filter.Trim(), StringComparison.OrdinalIgnoreCase);

    private static KnxLight ToKnxLight(Light light) => new()
    {
        GroupName = light.GroupName,
        Floor = light.Floor,
        Room = light.Room,
        Style = light.Style,
        Orientation = light.Orientation,
        Switch = light.IsOn is { } isOn ? isOn ? DptSwitch.On : DptSwitch.Off : null,
    };

    /// <summary>A synthetic KNX light.</summary>
    /// <param name="Floor">The floor.</param>
    /// <param name="Room">The room.</param>
    /// <param name="Style">The fitting style.</param>
    /// <param name="Orientation">The side of the house.</param>
    /// <param name="IsOn">The switch state, or <see langword="null"/> when none has been reported.</param>
    public sealed record Light(FloorType Floor, RoomType Room, LightStyle Style, CompassOrientation Orientation, bool? IsOn)
    {
        /// <summary>The KNX group name, for example <c>EG-LI-Kitchen-DL-North</c>.</summary>
        public string GroupName => $"{Floor}-LI-{Room}-{Style}-{Orientation}";
    }
}

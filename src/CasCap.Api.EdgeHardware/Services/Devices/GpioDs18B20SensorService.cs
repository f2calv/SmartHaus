using Iot.Device.OneWire;

namespace CasCap.Services;

/// <summary>
/// DS18B20 is a waterproof temperature sensor.
/// </summary>
public class GpioDs18B20SensorService(ILogger<GpioDs18B20SensorService> logger)
{

    //https://github.com/dotnet/iot/blob/ddb69e5e71b48c14a8ec37c63ad72a82d7f50652/src/devices/OneWire/README.md?plain=1#L3
    /// <summary>Reads temperature from the DS18B20 sensor.</summary>
    /// <param name="simple">When true, uses the simple enumeration method.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task GetTempInCelsius(bool simple = true)
    {
        // Make sure you can access the bus device before requesting a device scan (or run using sudo)
        // $ sudo chmod a+rw /sys/bus/w1/devices/w1_bus_master1/w1_master_*
        if (simple)
        {
            // Quick and simple way to find a thermometer and print the temperature
            foreach (var dev in OneWireThermometerDevice.EnumerateDevices())
            {
                var temp = await dev.ReadTemperatureAsync();
                logger.LogInformation("{ClassName} temperature reported by device {DeviceId}: {Temp:F2}",
                    nameof(GpioDs18B20SensorService), dev.DeviceId, temp.DegreesCelsius);
            }
        }
        else
        {
            // More advanced way, with rescanning the bus and iterating devices per 1-wire bus
            foreach (var busId in OneWireBus.EnumerateBusIds())
            {
                OneWireBus bus = new(busId);
                logger.LogInformation("{ClassName} found bus '{BusId}', scanning for devices ...",
                    nameof(GpioDs18B20SensorService), bus.BusId);
                await bus.ScanForDeviceChangesAsync();
                foreach (string devId in bus.EnumerateDeviceIds())
                {
                    OneWireDevice dev = new(busId, devId);
                    logger.LogInformation("{ClassName} found family '{Family}' device '{DeviceId}' on '{BusId}'",
                        nameof(GpioDs18B20SensorService), dev.Family, dev.DeviceId, bus.BusId);
                    if (OneWireThermometerDevice.IsCompatible(busId, devId))
                    {
                        OneWireThermometerDevice devTemp = new(busId, devId);
                        var temp = await devTemp.ReadTemperatureAsync();
                        logger.LogInformation("{ClassName} temperature reported by device: {Temp:F2}",
                            nameof(GpioDs18B20SensorService), temp.DegreesCelsius);
                    }
                }
            }
        }
    }
}

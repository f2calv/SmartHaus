using Iot.Device.Bmxx80;
using Iot.Device.Bmxx80.PowerMode;
using System.Device.I2c;

namespace CasCap.Services;

/// <summary>
/// BMP280 temperature and barometric pressure sensor.
/// https://cdn-shop.adafruit.com/datasheets/BST-BMP280-DS001-11.pdf
/// </summary>
public class GpioBmp280SensorService(ILogger<GpioBmp280SensorService> logger)
{

    /// <summary>Starts the BMP280 sensor monitoring loop.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} starting", nameof(GpioBmp280SensorService));
        await ReadFromSensor(cancellationToken);
        logger.LogInformation("{ClassName} exiting", nameof(GpioBmp280SensorService));
    }

    // bus id on the MCU
    const int BusId = 1;

    //from https://learn.microsoft.com/en-us/dotnet/iot/tutorials/temp-sensor
    /// <summary>Reads temperature and pressure data from the BMP280 sensor.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ReadFromSensor(CancellationToken cancellationToken)
    {
        var i2cSettings = new I2cConnectionSettings(BusId, Bme280.DefaultI2cAddress);
        logger.LogInformation("{ClassName} 1", nameof(GpioBmp280SensorService));
        using I2cDevice i2cDevice = I2cDevice.Create(i2cSettings);
        logger.LogInformation("{ClassName} 2", nameof(GpioBmp280SensorService));
        using var i2CBmp280 = new Bmp280(i2cDevice);
        logger.LogInformation("{ClassName} 3", nameof(GpioBmp280SensorService));

        //var measurementTime = i2CBmp280.GetMeasurementDuration();
        //_logger.LogInformation("{ClassName} measurementTime {measurementTime}",
        //    nameof(GpioBmp280SensorService), measurementTime);

        while (!cancellationToken.IsCancellationRequested)
        {
            //await Task.Delay(measurementTime, cancellationToken);

            //set mode forced so device sleeps after read
            i2CBmp280.SetPowerMode(Bmx280PowerMode.Forced);

            // set higher sampling
            i2CBmp280.TemperatureSampling = Sampling.UltraLowPower;
            i2CBmp280.PressureSampling = Sampling.UltraLowPower;

            // Perform a asynchronous measurement
            var readResult = await i2CBmp280.ReadAsync();
            if (readResult is null)
            {
                logger.LogWarning("{ClassName} ReadAsync is null",
                    nameof(GpioBmp280SensorService));
                continue;
            }

            // Print out the measured data
            logger.LogInformation("{ClassName} Pressure: {PreValue:#.##} hPa",
                nameof(GpioBmp280SensorService), readResult.Pressure?.Hectopascals);

            if (i2CBmp280.TryReadTemperature(out var tempValue))
                logger.LogInformation("{ClassName} Temperature: {TempValue:0.#}",
                    nameof(GpioBmp280SensorService), tempValue.DegreesCelsius);

            if (i2CBmp280.TryReadPressure(out var preValue))
                logger.LogInformation("{ClassName} Pressure: {PreValue:#.##} hPa",
                    nameof(GpioBmp280SensorService), preValue.Hectopascals);

            if (i2CBmp280.TryReadAltitude(out var altValue))
                logger.LogInformation("{ClassName} Estimated altitude: {AltValue:#} m",
                    nameof(GpioBmp280SensorService), altValue.Meters);

            await Task.Delay(1_000, cancellationToken);
        }
    }
}

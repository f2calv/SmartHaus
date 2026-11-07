using System.Device.Gpio;
using System.Device.Gpio.Drivers;

namespace CasCap.Services;

/// <summary>Creates sensor controllers using the libgpiod v2 ABI on Linux.</summary>
internal static class GpioControllerFactory
{
    /// <summary>Creates a controller with logical pin numbering and the platform's supported driver.</summary>
    /// <returns>A controller owned by the sensor.</returns>
    internal static GpioController Create()
    {
        if (!OperatingSystem.IsLinux())
            return new GpioController();

        var chip = SelectChip(LibGpiodV2Driver.GetAvailableChips());
        return new GpioController(new LibGpiodV2Driver(chip));
    }

    /// <summary>Selects the 54-line GPIO chip, or chip zero on other boards.</summary>
    /// <param name="chips">Available native GPIO chip descriptors.</param>
    /// <returns>The selected chip.</returns>
    /// <exception cref="NotSupportedException">No supported default GPIO chip is available.</exception>
    internal static GpioChipInfo SelectChip(IEnumerable<GpioChipInfo> chips)
        => chips.FirstOrDefault(chip => chip.NumLines == 54)
            ?? chips.FirstOrDefault(chip => chip.Id == 0)
            ?? throw new NotSupportedException("No supported default GPIO chip is available.");
}
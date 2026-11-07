using System.Device.Gpio;

namespace CasCap.Tests.Unit;

/// <summary>Verifies native GPIO chip selection without loading drivers or accessing hardware.</summary>
[Trait("Category", "Gpio")]
public sealed class GpioControllerFactoryTests
{
    [Fact]
    public void SelectChip_Prefers54LineChipOverChipZero()
    {
        GpioChipInfo[] chips = [new(0, "gpiochip0", "example", 32), new(4, "gpiochip4", "example", 54)];

        Assert.Equal(4, GpioControllerFactory.SelectChip(chips).Id);
    }

    [Fact]
    public void SelectChip_UsesChipZeroOnOtherBoards()
    {
        GpioChipInfo[] chips = [new(2, "gpiochip2", "example", 16), new(0, "gpiochip0", "example", 32)];

        Assert.Equal(0, GpioControllerFactory.SelectChip(chips).Id);
    }

    [Fact]
    public void SelectChip_RejectsMissingDefaultChip()
        => Assert.Throws<NotSupportedException>(() =>
            GpioControllerFactory.SelectChip([new(2, "gpiochip2", "example", 16)]));

    [Fact]
    public void SelectChip_RejectsEmptyChipList()
        => Assert.Throws<NotSupportedException>(() => GpioControllerFactory.SelectChip([]));
}
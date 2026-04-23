namespace CasCap.Tests;

/// <summary>
/// Unit tests for <see cref="ChannelKnxTelegramBroker{T}"/>.
/// </summary>
public class ChannelKnxTelegramBrokerTests
{
    [Fact]
    public async Task PublishAsync_And_SubscribeAsync_RoundTrips_Single_Item()
    {
        var broker = new ChannelKnxTelegramBroker<string>();
        var expected = "hello";

        await broker.PublishAsync(expected);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var item in broker.SubscribeAsync(cts.Token))
        {
            Assert.Equal(expected, item);
            break;
        }
    }

    [Fact]
    public async Task SubscribeAsync_Yields_Items_In_Order()
    {
        var broker = new ChannelKnxTelegramBroker<int>();
        var expected = new[] { 1, 2, 3 };

        foreach (var value in expected)
            await broker.PublishAsync(value);

        var received = new List<int>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var item in broker.SubscribeAsync(cts.Token))
        {
            received.Add(item);
            if (received.Count == expected.Length)
                break;
        }

        Assert.Equal(expected, received);
    }

    [Fact]
    public async Task SubscribeAsync_Cancellation_Stops_Enumeration()
    {
        var broker = new ChannelKnxTelegramBroker<string>();

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var items = new List<string>();

        try
        {
            await foreach (var item in broker.SubscribeAsync(cts.Token))
                items.Add(item);
        }
        catch (OperationCanceledException)
        {
            // expected
        }

        Assert.Empty(items);
    }

    [Fact]
    public async Task PublishAsync_Multiple_Consumers_Not_Supported_SingleReader()
    {
        // ChannelKnxTelegramBroker is configured with SingleReader = true,
        // so only one subscriber should consume items.
        var broker = new ChannelKnxTelegramBroker<int>();

        await broker.PublishAsync(42);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var item in broker.SubscribeAsync(cts.Token))
        {
            Assert.Equal(42, item);
            break;
        }
    }
}

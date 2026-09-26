using Microsoft.Extensions.Logging.Abstractions;

namespace CasCap.Tests.Unit;

/// <summary>
/// Tests for the reservation key <see cref="RedisSignalMessageDeduplicator"/> derives, which is the
/// part of duplicate suppression that must be deterministic and must not carry identifiers.
/// </summary>
[Trait("Category", "Comms")]
public class SignalMessageDeduplicatorTests
{
    private static SignalMessageIdentity Identity(string account = "+10000000000",
        string conversation = "group.ZXhhbXBsZQ==", string sender = "+10000000001", long timestamp = 1712153610000) =>
        new() { Account = account, Conversation = conversation, Sender = sender, Timestamp = timestamp };

    private static CommsAgentConfig Config() => new();

    private static RedisSignalMessageDeduplicator UnreachableSvc() => new(
        NullLogger<RedisSignalMessageDeduplicator>.Instance, Options.Create(Config()), new ThrowingRemoteCache());

    [Fact]
    public void BuildKey_IsStableForTheSameIdentity() =>
        Assert.Equal(RedisSignalMessageDeduplicator.BuildKey(Identity()),
            RedisSignalMessageDeduplicator.BuildKey(Identity()));

    [Theory]
    [InlineData("  +10000000000  ", "group.ZXhhbXBsZQ==", "+10000000001")]
    [InlineData("+10000000000", "GROUP.ZXhhbXBsZQ==", "+10000000001")]
    [InlineData("+10000000000", "group.ZXhhbXBsZQ==", "  +10000000001")]
    public void BuildKey_NormalizesCasingAndPadding(string account, string conversation, string sender) =>
        Assert.Equal(RedisSignalMessageDeduplicator.BuildKey(Identity()),
            RedisSignalMessageDeduplicator.BuildKey(Identity(account, conversation, sender)));

    [Fact]
    public void BuildKey_DiffersWhenTheTimestampDiffers() =>
        Assert.NotEqual(RedisSignalMessageDeduplicator.BuildKey(Identity()),
            RedisSignalMessageDeduplicator.BuildKey(Identity(timestamp: 1712153610001)));

    [Fact]
    public void BuildKey_DiffersWhenTheSenderDiffers() =>
        Assert.NotEqual(RedisSignalMessageDeduplicator.BuildKey(Identity()),
            RedisSignalMessageDeduplicator.BuildKey(Identity(sender: "+10000000002")));

    [Fact]
    public void BuildKey_IsAPrefixedSha256HexDigest()
    {
        var key = RedisSignalMessageDeduplicator.BuildKey(Identity());

        Assert.StartsWith(RedisSignalMessageDeduplicator.KeyPrefix, key, StringComparison.Ordinal);
        var digest = key[RedisSignalMessageDeduplicator.KeyPrefix.Length..];
        Assert.Equal(64, digest.Length);
        Assert.Matches("^[0-9a-f]{64}$", digest);
    }

    [Fact]
    public void BuildKey_CarriesNoAccountGroupOrSenderIdentifier()
    {
        var identity = Identity();
        var key = RedisSignalMessageDeduplicator.BuildKey(identity);

        //Only the digest may reach Redis; a raw identifier in the key would be readable by anyone
        //able to list keys.
        Assert.DoesNotContain(identity.Account, key, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(identity.Account.TrimStart('+'), key, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(identity.Conversation, key, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(identity.Sender.TrimStart('+'), key, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(identity.Timestamp.ToString(), key, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryClaimAsync_FailsOpenWhenTheStoreThrows()
    {
        //A cache outage must never silently stop inbound Signal messages.
        Assert.True(await UnreachableSvc().TryClaimAsync(Identity(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReleaseAsync_SwallowsStoreFailures() =>
        await UnreachableSvc().ReleaseAsync(Identity(), TestContext.Current.CancellationToken);

    [Fact]
    public void MessageDeduplicationTtlHours_DefaultsToSevenDays() =>
        Assert.Equal(TimeSpan.FromDays(7), TimeSpan.FromHours(Config().MessageDeduplicationTtlHours));
}

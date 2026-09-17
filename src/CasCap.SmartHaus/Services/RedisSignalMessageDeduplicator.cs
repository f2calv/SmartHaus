using StackExchange.Redis;
using System.Globalization;
using System.Security.Cryptography;

namespace CasCap.Services;

/// <summary>
/// Redis-backed <see cref="ISignalMessageDeduplicator"/> that reserves a hashed message identity
/// with <c>SET key value NX EX</c>.
/// </summary>
/// <remarks>
/// <para>
/// All raw Redis access for duplicate suppression is confined to this type; callers depend on
/// <see cref="ISignalMessageDeduplicator"/> only.
/// </para>
/// <para>
/// A reservation is a best-effort hint rather than an exactly-once guarantee — it bounds repeated
/// work, it does not make processing and its side effects atomic.
/// </para>
/// </remarks>
public sealed class RedisSignalMessageDeduplicator(
    ILogger<RedisSignalMessageDeduplicator> logger,
    IOptions<CommsAgentConfig> commsAgentConfig,
    IRemoteCache remoteCache) : ISignalMessageDeduplicator
{
    /// <summary>The Redis key prefix under which reservations are stored.</summary>
    public const string KeyPrefix = "comms:cache:signal-message:";

    /// <inheritdoc/>
    public async Task<bool> TryClaimAsync(SignalMessageIdentity identity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = BuildKey(identity);
        var ttl = TimeSpan.FromHours(commsAgentConfig.Value.MessageDeduplicationTtlHours);
        try
        {
            var claimed = await remoteCache.Db.StringSetAsync(key, 1, ttl, When.NotExists);
            if (!claimed)
                logger.LogInformation("{ClassName} duplicate suppressed for reservation {ReservationKey}",
                    nameof(RedisSignalMessageDeduplicator), key);
            return claimed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            //A store outage must not silently stop inbound messages, so fail open and process.
            logger.LogWarning(ex, "{ClassName} reservation failed for {ReservationKey}, processing anyway ({ExceptionType})",
                nameof(RedisSignalMessageDeduplicator), key, ex.GetType().Name);
            return true;
        }
    }

    /// <inheritdoc/>
    public async Task ReleaseAsync(SignalMessageIdentity identity, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = BuildKey(identity);
        try
        {
            await remoteCache.Db.KeyDeleteAsync(key);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "{ClassName} reservation release failed for {ReservationKey} ({ExceptionType})",
                nameof(RedisSignalMessageDeduplicator), key, ex.GetType().Name);
        }
    }

    /// <summary>Derives the reservation key for a message identity.</summary>
    /// <param name="identity">The stable coordinates of the inbound message.</param>
    /// <remarks>
    /// The identity is normalised (trimmed, lower-cased, unit-separator delimited) before hashing so
    /// that casing or padding differences between transports cannot produce two reservations for one
    /// message. Only the hash reaches Redis, so no phone number or group identifier is stored.
    /// </remarks>
    public static string BuildKey(SignalMessageIdentity identity)
    {
        var canonical = string.Join('\u001f',
            Normalize(identity.Account),
            Normalize(identity.Conversation),
            Normalize(identity.Sender),
            identity.Timestamp.ToString(CultureInfo.InvariantCulture));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return KeyPrefix + Convert.ToHexStringLower(hash);
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
}

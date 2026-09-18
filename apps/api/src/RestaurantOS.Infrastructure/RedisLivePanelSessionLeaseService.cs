using RestaurantOS.Application;
using StackExchange.Redis;

namespace RestaurantOS.Infrastructure;

/// <summary>
/// Redis-backed live-panel leases for multi-instance / scaled-out API hosts.
/// Uses a hash per branch with sliding TTL; expired fields are pruned on acquire.
/// </summary>
public sealed class RedisLivePanelSessionLeaseService(IConnectionMultiplexer multiplexer) : ILivePanelSessionLeaseService
{
    private static readonly TimeSpan LeaseTtl = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan KeyTtl = TimeSpan.FromHours(2);

    public async Task<LivePanelLeaseAcquireResult> TryAcquireAsync(
        Guid tenantId,
        Guid branchId,
        string connectionId,
        int? maxSessions,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        var db = multiplexer.GetDatabase();
        var key = RedisKey(tenantId, branchId);
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(LeaseTtl).UtcTicks;

        await PruneExpiredAsync(db, key, now.UtcTicks);

        if (await db.HashExistsAsync(key, connectionId))
        {
            await db.HashSetAsync(key, connectionId, expiresAt);
            await db.KeyExpireAsync(key, KeyTtl);
            var refreshedCount = (int)await db.HashLengthAsync(key);
            return new LivePanelLeaseAcquireResult(true, refreshedCount, maxSessions, null);
        }

        var activeCount = (int)await db.HashLengthAsync(key);
        if (maxSessions is int max && max > 0 && activeCount >= max)
        {
            return new LivePanelLeaseAcquireResult(
                false,
                activeCount,
                max,
                $"Ücretsiz planda canlı sipariş paneli aynı anda {max} cihazda açılabilir "
                + $"({activeCount}/{max}). Diğer oturumu kapatın veya Pro’ya geçerek ekibinizle birlikte takip edin.");
        }

        await db.HashSetAsync(key, connectionId, expiresAt);
        await db.KeyExpireAsync(key, KeyTtl);
        var nextCount = (int)await db.HashLengthAsync(key);
        return new LivePanelLeaseAcquireResult(true, nextCount, maxSessions, null);
    }

    public async Task ReleaseAsync(
        Guid tenantId,
        Guid branchId,
        string connectionId,
        CancellationToken cancellationToken)
    {
        var db = multiplexer.GetDatabase();
        var key = RedisKey(tenantId, branchId);
        await db.HashDeleteAsync(key, connectionId);
    }

    private static RedisKey RedisKey(Guid tenantId, Guid branchId) =>
        $"live-panel:{tenantId:N}:{branchId:N}";

    private static async Task PruneExpiredAsync(IDatabase db, RedisKey key, long nowTicks)
    {
        var entries = await db.HashGetAllAsync(key);
        if (entries.Length == 0)
        {
            return;
        }

        var stale = new List<RedisValue>();
        foreach (var entry in entries)
        {
            if (!entry.Value.TryParse(out long expiresAt) || expiresAt <= nowTicks)
            {
                stale.Add(entry.Name);
            }
        }

        if (stale.Count > 0)
        {
            await db.HashDeleteAsync(key, stale.ToArray());
        }
    }
}

using System.Collections.Concurrent;
using RestaurantOS.Application;

namespace RestaurantOS.Infrastructure;

/// <summary>
/// Process-local live-panel session tracker. Suitable for single-instance deployments.
/// Multi-instance deployments should register <see cref="RedisLivePanelSessionLeaseService"/>.
/// </summary>
public sealed class InMemoryLivePanelSessionLeaseService : ILivePanelSessionLeaseService
{
    private static readonly TimeSpan LeaseTtl = TimeSpan.FromMinutes(45);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, long>> _sessions = new(StringComparer.Ordinal);

    public Task<LivePanelLeaseAcquireResult> TryAcquireAsync(
        Guid tenantId,
        Guid branchId,
        string connectionId,
        int? maxSessions,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        var bucketKey = BucketKey(tenantId, branchId);
        var bucket = _sessions.GetOrAdd(bucketKey, _ => new ConcurrentDictionary<string, long>(StringComparer.Ordinal));
        PruneExpired(bucket);

        var now = DateTimeOffset.UtcNow.UtcTicks;
        var expires = DateTimeOffset.UtcNow.Add(LeaseTtl).UtcTicks;

        if (bucket.ContainsKey(connectionId))
        {
            bucket[connectionId] = expires;
            return Task.FromResult(new LivePanelLeaseAcquireResult(true, bucket.Count, maxSessions, null));
        }

        if (maxSessions is int max && max > 0 && bucket.Count >= max)
        {
            return Task.FromResult(new LivePanelLeaseAcquireResult(
                false,
                bucket.Count,
                max,
                $"Ücretsiz planda canlı sipariş paneli aynı anda {max} cihazda açılabilir "
                + $"({bucket.Count}/{max}). Diğer oturumu kapatın veya Pro’ya geçerek ekibinizle birlikte takip edin."));
        }

        bucket[connectionId] = expires;
        return Task.FromResult(new LivePanelLeaseAcquireResult(true, bucket.Count, maxSessions, null));
    }

    public Task ReleaseAsync(
        Guid tenantId,
        Guid branchId,
        string connectionId,
        CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(BucketKey(tenantId, branchId), out var bucket))
        {
            bucket.TryRemove(connectionId, out _);
            if (bucket.IsEmpty)
            {
                _sessions.TryRemove(BucketKey(tenantId, branchId), out _);
            }
        }

        return Task.CompletedTask;
    }

    private static string BucketKey(Guid tenantId, Guid branchId) =>
        $"{tenantId:N}:{branchId:N}";

    private static void PruneExpired(ConcurrentDictionary<string, long> bucket)
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;
        foreach (var pair in bucket)
        {
            if (pair.Value <= now)
            {
                bucket.TryRemove(pair.Key, out _);
            }
        }
    }
}

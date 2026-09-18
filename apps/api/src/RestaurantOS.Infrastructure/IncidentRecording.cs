using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class IncidentDraftChannel
{
    public Channel<IncidentDraft> Channel { get; }

    public IncidentDraftChannel(IOptions<IncidentRecorderOptions> options)
    {
        var capacity = Math.Clamp(options.Value.QueueCapacity, 64, 16_384);
        Channel = System.Threading.Channels.Channel.CreateBounded<IncidentDraft>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });
    }
}

public sealed partial class ChannelIncidentRecorder(
    IncidentDraftChannel queue,
    IOptions<IncidentRecorderOptions> options,
    IMemoryCache cache,
    ILogger<ChannelIncidentRecorder> logger) : IIncidentRecorder
{
    public void Record(IncidentDraft draft)
    {
        var settings = options.Value;
        if (!settings.Enabled
            || string.IsNullOrWhiteSpace(draft.Code)
            || string.IsNullOrWhiteSpace(draft.Message))
        {
            return;
        }

        try
        {
            var dedupeKey = BuildDedupeKey(draft);
            if (dedupeKey is not null
                && settings.DedupeWindowSeconds > 0
                && cache.TryGetValue(dedupeKey, out _))
            {
                return;
            }

            if (dedupeKey is not null && settings.DedupeWindowSeconds > 0)
            {
                cache.Set(
                    dedupeKey,
                    1,
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(settings.DedupeWindowSeconds),
                    });
            }

            if (!queue.Channel.Writer.TryWrite(draft with { DetailJson = SanitizeDetailJson(draft.DetailJson) }))
            {
                LogQueueRejected(logger, draft.Code);
            }
        }
        catch (Exception exception)
        {
            LogEnqueueFailed(logger, exception, draft.Code);
        }
    }

    [LoggerMessage(EventId = 5201, Level = LogLevel.Debug, Message = "Incident channel rejected draft {Code}")]
    private static partial void LogQueueRejected(ILogger logger, string code);

    [LoggerMessage(EventId = 5202, Level = LogLevel.Warning, Message = "Failed to enqueue incident {Code}")]
    private static partial void LogEnqueueFailed(ILogger logger, Exception exception, string code);

    private static string? BuildDedupeKey(IncidentDraft draft)
    {
        var scope = draft.CustomerSessionId?.ToString("N")
            ?? draft.SessionTokenForLookup
            ?? draft.ClientDeviceId
            ?? draft.ClientIp
            ?? "anon";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope)))[..16];
        return $"incident-dedupe:{draft.TenantId:N}:{draft.BranchId:N}:{draft.Code}:{hash}";
    }

    private static string? SanitizeDetailJson(string? detailJson)
    {
        if (string.IsNullOrWhiteSpace(detailJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(detailJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var safe = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var name = property.Name;
                if (IsSensitiveProperty(name))
                {
                    continue;
                }

                safe[name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => Truncate(property.Value.GetString(), 200),
                    JsonValueKind.Number => property.Value.TryGetInt64(out var number) ? number : property.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    JsonValueKind.Array => property.Value.GetArrayLength() <= 20
                        ? property.Value.EnumerateArray()
                            .Take(20)
                            .Select(item => item.ToString())
                            .ToArray()
                        : $"[array:{property.Value.GetArrayLength()}]",
                    _ => Truncate(property.Value.ToString(), 200),
                };
            }

            return JsonSerializer.Serialize(safe);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsSensitiveProperty(string name) =>
        name.Contains("token", StringComparison.OrdinalIgnoreCase)
        || name.Contains("password", StringComparison.OrdinalIgnoreCase)
        || name.Contains("secret", StringComparison.OrdinalIgnoreCase)
        || name.Contains("authorization", StringComparison.OrdinalIgnoreCase)
        || name.Contains("cookie", StringComparison.OrdinalIgnoreCase)
        || name.Contains("note", StringComparison.OrdinalIgnoreCase);

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];
}

public sealed partial class IncidentEventWriterHostedService(
    IncidentDraftChannel queue,
    IServiceScopeFactory scopeFactory,
    IOptions<IncidentRecorderOptions> options,
    TimeProvider timeProvider,
    ILogger<IncidentEventWriterHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = queue.Channel.Reader;
        var batch = new List<IncidentDraft>(Math.Max(8, options.Value.WriterBatchSize));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                batch.Clear();
                var first = await reader.ReadAsync(stoppingToken);
                batch.Add(first);
                while (batch.Count < options.Value.WriterBatchSize && reader.TryRead(out var next))
                {
                    batch.Add(next);
                }

                await PersistBatchAsync(batch, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogWriterFailed(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task PersistBatchAsync(List<IncidentDraft> drafts, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var expires = now.AddDays(Math.Clamp(settings.RetentionDays, 7, 730));

        var entities = new List<IncidentEvent>(drafts.Count);
        foreach (var draft in drafts)
        {
            var entity = new IncidentEvent(
                Guid.NewGuid(),
                now,
                expires,
                draft.Channel.Trim().ToLowerInvariant(),
                ResolveSeverity(draft).ToLowerInvariant(),
                draft.Code.Trim().ToUpperInvariant(),
                draft.Message,
                draft.ActorType.Trim().ToLowerInvariant(),
                draft.TenantId,
                draft.BranchId,
                draft.RestaurantId,
                draft.HttpStatus,
                draft.CorrelationId,
                draft.ActorUserId,
                draft.CustomerSessionId,
                draft.GuestSessionId,
                draft.TableId,
                draft.OrderId,
                HashOptional(draft.ClientDeviceId),
                HashOptional(draft.ClientIp),
                draft.RequestMethod,
                draft.RequestPath,
                draft.DetailJson,
                BuildPersistedDedupeKey(draft));

            await EnrichFromSessionAsync(db, entity, draft.SessionTokenForLookup, cancellationToken);
            entities.Add(entity);
        }

        db.IncidentEvents.AddRange(entities);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnrichFromSessionAsync(
        RestaurantOsDbContext db,
        IncidentEvent entity,
        string? sessionToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken)
            || entity.TenantId is not null)
        {
            return;
        }

        var hash = OpaqueToken.Hash(sessionToken);
        var session = await db.CustomerSessions
            .AsNoTracking()
            .Where(entry => entry.TokenHash == hash)
            .Select(entry => new
            {
                entry.Id,
                entry.TenantId,
                entry.BranchId,
                entry.TableId,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null)
        {
            return;
        }

        var restaurantId = await db.Branches
            .AsNoTracking()
            .Where(branch => branch.Id == session.BranchId && branch.TenantId == session.TenantId)
            .Select(branch => (Guid?)branch.RestaurantId)
            .SingleOrDefaultAsync(cancellationToken);

        var guestSessionId = await db.GuestSessions
            .AsNoTracking()
            .Where(guest => guest.TableSessionId == session.Id && guest.Status == GuestSessionStatus.Active)
            .OrderByDescending(guest => guest.CreatedAtUtc)
            .Select(guest => (Guid?)guest.Id)
            .FirstOrDefaultAsync(cancellationToken);

        entity.EnrichScope(
            session.TenantId,
            session.BranchId,
            restaurantId,
            session.Id,
            guestSessionId,
            session.TableId);
    }

    private static string ResolveSeverity(IncidentDraft draft)
    {
        if (!string.IsNullOrWhiteSpace(draft.Severity))
        {
            return draft.Severity;
        }

        return draft.Code.ToUpperInvariant() switch
        {
            "ORDER_BLOCKED" or "UNHANDLED_EXCEPTION" => IncidentSeverities.Critical,
            "ORDER_RATE_LIMITED" or "ORDER_REJECTED" or "MENU_UNAVAILABLE" or "SERVICE_REQUEST_OPEN"
                or "SERVICE_REQUEST_COOLDOWN" or "IDEMPOTENCY_CONFLICT" => IncidentSeverities.Warning,
            "INVALID_QR" or "INVALID_SESSION" => IncidentSeverities.Info,
            _ when draft.HttpStatus >= 500 => IncidentSeverities.Error,
            _ when draft.HttpStatus >= 400 => IncidentSeverities.Warning,
            _ => IncidentSeverities.Error,
        };
    }

    private static string? HashOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : OpaqueToken.Hash(value.Trim().ToLowerInvariant());

    private static string? BuildPersistedDedupeKey(IncidentDraft draft)
    {
        if (draft.CustomerSessionId is null && string.IsNullOrWhiteSpace(draft.SessionTokenForLookup))
        {
            return null;
        }

        var scope = draft.CustomerSessionId?.ToString("N")
            ?? OpaqueToken.Hash(draft.SessionTokenForLookup!);
        return $"{draft.Code}:{scope}"[..Math.Min(160, $"{draft.Code}:{scope}".Length)];
    }

    [LoggerMessage(EventId = 5211, Level = LogLevel.Error, Message = "Incident writer batch failed")]
    private static partial void LogWriterFailed(ILogger logger, Exception exception);
}

public sealed partial class IncidentEventPurgeHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<IncidentRecorderOptions> options,
    TimeProvider timeProvider,
    ILogger<IncidentEventPurgeHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stagger startup so writers settle first.
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogPurgeFailed(logger, exception);
            }

            var delay = TimeSpan.FromMinutes(Math.Clamp(options.Value.PurgeIntervalMinutes, 15, 24 * 60));
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task PurgeOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        var now = timeProvider.GetUtcNow();
        var batchSize = Math.Clamp(options.Value.PurgeBatchSize, 50, 5_000);
        var total = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var expiredIds = await db.IncidentEvents
                .AsNoTracking()
                .Where(entry => entry.ExpiresAtUtc <= now)
                .OrderBy(entry => entry.ExpiresAtUtc)
                .Select(entry => entry.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
            if (expiredIds.Count == 0)
            {
                break;
            }

            await db.IncidentEvents
                .Where(entry => expiredIds.Contains(entry.Id))
                .ExecuteDeleteAsync(cancellationToken);
            total += expiredIds.Count;
            if (expiredIds.Count < batchSize)
            {
                break;
            }
        }

        if (total > 0)
        {
            LogPurged(logger, total);
        }
    }

    [LoggerMessage(EventId = 5221, Level = LogLevel.Error, Message = "Incident purge failed")]
    private static partial void LogPurgeFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5222, Level = LogLevel.Information, Message = "Purged {Count} expired incident events")]
    private static partial void LogPurged(ILogger logger, int count);
}

public sealed class IncidentQueryService(RestaurantOsDbContext dbContext) : IIncidentQueryService
{
    public async Task<IReadOnlyList<IncidentEventResult>> QueryAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? code,
        string? channel,
        string? severity,
        Guid? tableId,
        Guid? orderId,
        string? correlationId,
        int limit,
        CancellationToken cancellationToken)
    {
        var allowed = await dbContext.ManagementMemberships
            .AsNoTracking()
            .AnyAsync(
                membership =>
                    membership.UserId == userId
                    && membership.TenantId == tenantId
                    && membership.BranchId == branchId
                    && membership.IsActive
                    && dbContext.ManagementRolePermissions.Any(grant =>
                        grant.RoleId == membership.RoleId
                        && grant.Permission == ManagementPermissions.OrderView),
                cancellationToken);
        if (!allowed)
        {
            throw new ManagementAuthException("FORBIDDEN", "Missing Order.View permission.");
        }

        var take = Math.Clamp(limit, 1, 200);
        var from = (fromUtc ?? DateTimeOffset.UtcNow.AddDays(-7)).ToUniversalTime();
        var to = (toUtc ?? DateTimeOffset.UtcNow.AddMinutes(5)).ToUniversalTime();
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var query = dbContext.IncidentEvents
            .AsNoTracking()
            .Where(entry =>
                entry.TenantId == tenantId
                && entry.BranchId == branchId
                && entry.OccurredAtUtc >= from
                && entry.OccurredAtUtc <= to
                && entry.ExpiresAtUtc > DateTimeOffset.UtcNow);

        if (!string.IsNullOrWhiteSpace(code))
        {
            var normalized = code.Trim().ToUpperInvariant();
            query = query.Where(entry => entry.Code == normalized);
        }

        if (!string.IsNullOrWhiteSpace(channel))
        {
            var normalized = channel.Trim().ToLowerInvariant();
            query = query.Where(entry => entry.Channel == normalized);
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            var normalized = severity.Trim().ToLowerInvariant();
            query = query.Where(entry => entry.Severity == normalized);
        }

        if (tableId is Guid tableFilter)
        {
            query = query.Where(entry => entry.TableId == tableFilter);
        }

        if (orderId is Guid orderFilter)
        {
            query = query.Where(entry => entry.OrderId == orderFilter);
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            var normalized = correlationId.Trim();
            query = query.Where(entry => entry.CorrelationId == normalized);
        }

        var rows = await query
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows.Select(entry => new IncidentEventResult(
                entry.Id,
                entry.OccurredAtUtc,
                entry.ExpiresAtUtc,
                entry.TenantId,
                entry.BranchId,
                entry.RestaurantId,
                entry.Channel,
                entry.Severity,
                entry.Code,
                entry.HttpStatus,
                entry.Message,
                entry.CorrelationId,
                entry.ActorType,
                entry.ActorUserId,
                entry.CustomerSessionId,
                entry.GuestSessionId,
                entry.TableId,
                entry.OrderId,
                entry.RequestMethod,
                entry.RequestPath,
                entry.DetailJson))
            .ToArray();
    }
}

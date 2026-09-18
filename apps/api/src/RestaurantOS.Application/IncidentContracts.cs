namespace RestaurantOS.Application;

public sealed class IncidentRecorderOptions
{
    public const string SectionName = "Incidents";

    /// <summary>How long incident rows stay queryable before purge.</summary>
    public int RetentionDays { get; init; } = 180;

    public bool Enabled { get; init; } = true;

    /// <summary>In-process queue capacity; overflow drops oldest drafts.</summary>
    public int QueueCapacity { get; init; } = 2048;

    /// <summary>Suppress identical code+scope events within this window (seconds).</summary>
    public int DedupeWindowSeconds { get; init; } = 60;

    public int WriterBatchSize { get; init; } = 32;

    public int PurgeBatchSize { get; init; } = 500;

    public int PurgeIntervalMinutes { get; init; } = 60;
}

/// <summary>Fire-and-forget operational incident capture (never throws to callers).</summary>
public interface IIncidentRecorder
{
    void Record(IncidentDraft draft);
}

public sealed record IncidentDraft(
    string Channel,
    string Code,
    string Message,
    string ActorType,
    string? Severity = null,
    int? HttpStatus = null,
    string? CorrelationId = null,
    Guid? TenantId = null,
    Guid? BranchId = null,
    Guid? RestaurantId = null,
    Guid? ActorUserId = null,
    Guid? CustomerSessionId = null,
    Guid? GuestSessionId = null,
    Guid? TableId = null,
    Guid? OrderId = null,
    string? ClientDeviceId = null,
    string? ClientIp = null,
    string? RequestMethod = null,
    string? RequestPath = null,
    string? DetailJson = null,
    /// <summary>Opaque session token used only for async scope enrichment; never persisted.</summary>
    string? SessionTokenForLookup = null);

public sealed record IncidentEventResult(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset ExpiresAtUtc,
    Guid? TenantId,
    Guid? BranchId,
    Guid? RestaurantId,
    string Channel,
    string Severity,
    string Code,
    int? HttpStatus,
    string Message,
    string? CorrelationId,
    string ActorType,
    Guid? ActorUserId,
    Guid? CustomerSessionId,
    Guid? GuestSessionId,
    Guid? TableId,
    Guid? OrderId,
    string? RequestMethod,
    string? RequestPath,
    string? DetailJson);

public interface IIncidentQueryService
{
    Task<IReadOnlyList<IncidentEventResult>> QueryAsync(
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
        CancellationToken cancellationToken);
}

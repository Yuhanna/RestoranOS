namespace RestaurantOS.Domain;

/// <summary>
/// Durable operational incident for support triage (not security audit).
/// Retention is enforced via <see cref="ExpiresAtUtc"/> (default 180 days).
/// </summary>
public sealed class IncidentEvent
{
    private IncidentEvent()
    {
    }

    public IncidentEvent(
        Guid id,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset expiresAtUtc,
        string channel,
        string severity,
        string code,
        string message,
        string actorType,
        Guid? tenantId = null,
        Guid? branchId = null,
        Guid? restaurantId = null,
        int? httpStatus = null,
        string? correlationId = null,
        Guid? actorUserId = null,
        Guid? customerSessionId = null,
        Guid? guestSessionId = null,
        Guid? tableId = null,
        Guid? orderId = null,
        string? clientFingerprintHash = null,
        string? clientIpHash = null,
        string? requestMethod = null,
        string? requestPath = null,
        string? detailJson = null,
        string? dedupeKey = null)
    {
        Id = id;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        ExpiresAtUtc = expiresAtUtc.ToUniversalTime();
        Channel = Required(channel, 32);
        Severity = Required(severity, 16);
        Code = Required(code, 80);
        Message = Required(message, 500);
        ActorType = Required(actorType, 32);
        TenantId = tenantId;
        BranchId = branchId;
        RestaurantId = restaurantId;
        HttpStatus = httpStatus;
        CorrelationId = Truncate(correlationId, 64);
        ActorUserId = actorUserId;
        CustomerSessionId = customerSessionId;
        GuestSessionId = guestSessionId;
        TableId = tableId;
        OrderId = orderId;
        ClientFingerprintHash = Truncate(clientFingerprintHash, 64);
        ClientIpHash = Truncate(clientIpHash, 64);
        RequestMethod = Truncate(requestMethod, 16);
        RequestPath = Truncate(requestPath, 256);
        DetailJson = Truncate(detailJson, 4000);
        DedupeKey = Truncate(dedupeKey, 160);
    }

    public Guid Id { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid? BranchId { get; private set; }
    public Guid? RestaurantId { get; private set; }
    public string Channel { get; private set; } = null!;
    public string Severity { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public int? HttpStatus { get; private set; }
    public string Message { get; private set; } = null!;
    public string? CorrelationId { get; private set; }
    public string ActorType { get; private set; } = null!;
    public Guid? ActorUserId { get; private set; }
    public Guid? CustomerSessionId { get; private set; }
    public Guid? GuestSessionId { get; private set; }
    public Guid? TableId { get; private set; }
    public Guid? OrderId { get; private set; }
    public string? ClientFingerprintHash { get; private set; }
    public string? ClientIpHash { get; private set; }
    public string? RequestMethod { get; private set; }
    public string? RequestPath { get; private set; }
    public string? DetailJson { get; private set; }
    public string? DedupeKey { get; private set; }

    public void EnrichScope(
        Guid? tenantId,
        Guid? branchId,
        Guid? restaurantId,
        Guid? customerSessionId,
        Guid? guestSessionId,
        Guid? tableId)
    {
        TenantId ??= tenantId;
        BranchId ??= branchId;
        RestaurantId ??= restaurantId;
        CustomerSessionId ??= customerSessionId;
        GuestSessionId ??= guestSessionId;
        TableId ??= tableId;
    }

    private static string Required(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.");
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

public static class IncidentChannels
{
    public const string Customer = "customer";
    public const string Management = "management";
    public const string System = "system";
}

public static class IncidentSeverities
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Error = "error";
    public const string Critical = "critical";
}

public static class IncidentActorTypes
{
    public const string Guest = "guest";
    public const string Staff = "staff";
    public const string System = "system";
    public const string Anonymous = "anonymous";
}

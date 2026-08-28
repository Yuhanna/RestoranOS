namespace RestaurantOS.Domain;

public enum ServiceRequestType
{
    Waiter = 0,
    Bill = 1,
    Water = 2,
    Cutlery = 3,
    Napkin = 4,
    Other = 5,
}

public enum ServiceRequestStatus
{
    Open = 0,
    Completed = 1,
    Cancelled = 2,
}

public sealed class ServiceRequest
{
    private ServiceRequest() { }

    public ServiceRequest(
        Guid id,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        Guid customerSessionId,
        ServiceRequestType type,
        DateTimeOffset createdAtUtc,
        string? note = null)
    {
        Id = id;
        TenantId = tenantId;
        BranchId = branchId;
        TableId = tableId;
        CustomerSessionId = customerSessionId;
        Type = type;
        Status = ServiceRequestStatus.Open;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (Note?.Length > 160)
        {
            throw new ArgumentException("Note is too long.");
        }
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid TableId { get; private set; }
    public Guid CustomerSessionId { get; private set; }
    public ServiceRequestType Type { get; private set; }
    public ServiceRequestStatus Status { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public void Complete(DateTimeOffset completedAtUtc)
    {
        if (Status != ServiceRequestStatus.Open)
        {
            throw new InvalidOperationException("Only open service requests can be completed.");
        }

        Status = ServiceRequestStatus.Completed;
        CompletedAtUtc = completedAtUtc.ToUniversalTime();
    }

    public static bool TryParseType(string? value, out ServiceRequestType type)
    {
        type = ServiceRequestType.Waiter;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out type);
    }

    public static string ToApiCode(ServiceRequestType type) => type.ToString().ToLowerInvariant();

    public static string ToApiStatus(ServiceRequestStatus status) => status.ToString().ToLowerInvariant();
}

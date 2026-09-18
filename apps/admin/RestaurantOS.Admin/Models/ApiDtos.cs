namespace RestaurantOS.Admin.Models;

public sealed class CatalogResponse
{
    public IReadOnlyList<CatalogProductResponse> Products { get; set; } = [];
    public string Note { get; set; } = string.Empty;
}

public sealed class CatalogProductResponse
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductKind { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public IReadOnlyList<PlanPriceResponse> Prices { get; set; } = [];
}

public sealed class PlanPriceResponse
{
    public Guid Id { get; set; }
    public string Interval { get; set; } = string.Empty;
    public string Currency { get; set; } = "TRY";
    public long AmountMinor { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class CampaignResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TargetPlanCode { get; set; } = string.Empty;
    public int DiscountPercent { get; set; }
    public int DurationMonths { get; set; }
    public bool IsActive { get; set; }
}

public sealed class NotificationResponse
{
    public Guid Id { get; set; }
    public string Audience { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? LastDispatchedAtUtc { get; set; }
}

public sealed class NotificationDispatchResponse
{
    public int EmailSentCount { get; set; }
    public int PushSentCount { get; set; }
    public IReadOnlyList<string> RecipientEmails { get; set; } = [];
}

public sealed class StaffResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset GrantedAtUtc { get; set; }
}

public sealed class TenantListResponse
{
    public IReadOnlyList<TenantListItemResponse> Items { get; set; } = [];
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
}

public sealed class TenantListItemResponse
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string RestaurantName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public bool IsTrial { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public int ActiveBranchCount { get; set; }
    public int? MaxBranches { get; set; }
    public int OpenQuoteCount { get; set; }
}

public sealed class TenantDetailResponse
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string RestaurantName { get; set; } = string.Empty;
    public string? BillingEmail { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public bool IsTrial { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public int PurchasedBranchAddonCount { get; set; }
    public int ActiveBranchCount { get; set; }
    public int FrozenBranchCount { get; set; }
    public int ActiveUserCount { get; set; }
    public int? MaxBranches { get; set; }
    public int? MaxActiveUsers { get; set; }
    public int MaxOrderHistoryHours { get; set; }
    public int? MaxActiveQrCodes { get; set; }
    public int? OverrideMaxBranches { get; set; }
    public int? OverrideMaxActiveUsers { get; set; }
    public int? OverrideMaxOrderHistoryHours { get; set; }
    public int? OverrideMaxActiveQrCodes { get; set; }
    public string? ContractNote { get; set; }
}

public sealed class QuoteListItemResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public int EstimatedBranchCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class QuoteDetailResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int EstimatedBranchCount { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
}

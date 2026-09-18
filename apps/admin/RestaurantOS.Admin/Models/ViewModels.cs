using System.ComponentModel.DataAnnotations;

namespace RestaurantOS.Admin.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "E-posta gerekli.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola gerekli.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}

public sealed class CatalogPageViewModel
{
    public string? Note { get; set; }
    public string? RoleCode { get; set; }
    public string? Email { get; set; }
    public IReadOnlyList<CatalogProductViewModel> Products { get; set; } = [];
    public CreatePriceViewModel Create { get; set; } = new();
}

public sealed class CatalogProductViewModel
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductKind { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public IReadOnlyList<PlanPriceViewModel> Prices { get; set; } = [];
}

public sealed class PlanPriceViewModel
{
    public Guid Id { get; set; }
    public string Interval { get; set; } = string.Empty;
    public string Currency { get; set; } = "TRY";
    public long AmountMinor { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class CreatePriceViewModel
{
    [Required(ErrorMessage = "Ürün gerekli.")]
    public string ProductCode { get; set; } = "Pro";

    [Required(ErrorMessage = "Aralık gerekli.")]
    public string Interval { get; set; } = "month";

    public string Amount { get; set; } = "2499";
}

public sealed class CampaignsPageViewModel
{
    public string? RoleCode { get; set; }
    public IReadOnlyList<CampaignViewModel> Offers { get; set; } = [];
    public CreateCampaignViewModel Create { get; set; } = new();
}

public sealed class CampaignViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TargetPlanCode { get; set; } = string.Empty;
    public int DiscountPercent { get; set; }
    public int DurationMonths { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateCampaignViewModel
{
    [Required(ErrorMessage = "Başlık gerekli.")]
    public string Title { get; set; } = "Pro geçiş kampanyası";

    [Required(ErrorMessage = "Metin gerekli.")]
    public string Body { get; set; } = "3 ay %20 indirim.";

    [Range(1, 100, ErrorMessage = "İndirim 1–100 arasında olmalı.")]
    public int DiscountPercent { get; set; } = 20;

    [Range(1, 36, ErrorMessage = "Süre 1–36 ay olmalı.")]
    public int DurationMonths { get; set; } = 3;
}

public sealed class OverviewViewModel
{
    public string? Email { get; set; }
    public string? RoleCode { get; set; }
    public int PublishedPriceCount { get; set; }
    public int OfferCount { get; set; }
    public int ActiveNotificationCount { get; set; }
    public int OperatorCount { get; set; }
    public int OpenQuoteCount { get; set; }
}

public sealed class NotificationsPageViewModel
{
    public string? RoleCode { get; set; }
    public IReadOnlyList<NotificationViewModel> Items { get; set; } = [];
    public CreateNotificationViewModel Create { get; set; } = new();
}

public sealed class NotificationViewModel
{
    public Guid Id { get; set; }
    public string Audience { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public DateTimeOffset? LastDispatchedAtUtc { get; set; }
}

public sealed class CreateNotificationViewModel
{
    [Required(ErrorMessage = "Başlık gerekli.")]
    [MaxLength(160, ErrorMessage = "Başlık en fazla 160 karakter.")]
    public string Title { get; set; } = "Pasa duyurusu";

    [Required(ErrorMessage = "Metin gerekli.")]
    [MaxLength(2000, ErrorMessage = "Metin en fazla 2000 karakter.")]
    public string Body { get; set; } = "Restoran paneline gidecek duyuru metni.";

    public string Audience { get; set; } = "non_pro";
    public string? ActionUrl { get; set; }
    public string? EndsAtLocal { get; set; }
    public bool DispatchNow { get; set; } = true;
}

public sealed class StaffPageViewModel
{
    public string? RoleCode { get; set; }
    public Guid CurrentUserId { get; set; }
    public int ActiveOwnerCount { get; set; }
    public IReadOnlyList<StaffViewModel> Members { get; set; } = [];
    public InviteStaffViewModel Invite { get; set; } = new();

    public bool IsLastActiveOwner(StaffViewModel member) =>
        member.IsActive
        && string.Equals(member.RoleCode, "Owner", StringComparison.OrdinalIgnoreCase)
        && ActiveOwnerCount <= 1;
}

public sealed class StaffViewModel
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset GrantedAtUtc { get; set; }
}

public sealed class InviteStaffViewModel
{
    [Required(ErrorMessage = "E-posta gerekli.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Rol gerekli.")]
    public string RoleCode { get; set; } = "Support";

    public string Password { get; set; } = string.Empty;
}

public sealed class TenantsPageViewModel
{
    public string? RoleCode { get; set; }
    public string? Query { get; set; }
    public string? Plan { get; set; }
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 50;
    public IReadOnlyList<TenantListItemViewModel> Items { get; set; } = [];
}

public sealed class TenantListItemViewModel
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

public sealed class TenantDetailPageViewModel
{
    public string? RoleCode { get; set; }
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string RestaurantName { get; set; } = string.Empty;
    public string? BillingEmail { get; set; }
    public bool IsTrial { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public int ActiveBranchCount { get; set; }
    public int FrozenBranchCount { get; set; }
    public int ActiveUserCount { get; set; }
    public int? MaxBranches { get; set; }
    public int? MaxActiveUsers { get; set; }
    public int MaxOrderHistoryHours { get; set; }
    public int? MaxActiveQrCodes { get; set; }
    public TenantSubscriptionFormViewModel Form { get; set; } = new();
}

public sealed class TenantSubscriptionFormViewModel
{
    [Required(ErrorMessage = "Plan gerekli.")]
    public string PlanCode { get; set; } = "Free";

    public string? ExpiresAtLocal { get; set; }

    [Range(0, 10000, ErrorMessage = "Ek şube 0–10000 arasında olmalı.")]
    public int PurchasedBranchAddonCount { get; set; }

    public int? OverrideMaxBranches { get; set; }
    public int? OverrideMaxActiveUsers { get; set; }
    public int? OverrideMaxOrderHistoryHours { get; set; }
    public int? OverrideMaxActiveQrCodes { get; set; }

    [MaxLength(2000, ErrorMessage = "Sözleşme notu en fazla 2000 karakter.")]
    public string? ContractNote { get; set; }
}

public sealed class QuotesPageViewModel
{
    public string? RoleCode { get; set; }
    public string Status { get; set; } = "Open";
    public IReadOnlyList<QuoteListItemViewModel> Items { get; set; } = [];
}

public sealed class QuoteListItemViewModel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public int EstimatedBranchCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class QuoteDetailPageViewModel
{
    public string? RoleCode { get; set; }
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
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? DecisionNote { get; set; }
    public QuoteDecisionFormViewModel Decision { get; set; } = new();
}

public sealed class QuoteDecisionFormViewModel
{
    public int? OverrideMaxBranches { get; set; }
    public int? OverrideMaxActiveUsers { get; set; }
    public int? OverrideMaxOrderHistoryHours { get; set; }
    public int? OverrideMaxActiveQrCodes { get; set; }
    public string? ExpiresAtLocal { get; set; }

    [MaxLength(2000, ErrorMessage = "Sözleşme notu en fazla 2000 karakter.")]
    public string? ContractNote { get; set; }

    [MaxLength(2000, ErrorMessage = "Karar notu en fazla 2000 karakter.")]
    public string? DecisionNote { get; set; }
}

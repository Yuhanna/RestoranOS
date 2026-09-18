using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace RestaurantOS.Web.Models;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "E-posta gerekli.")]
    [EmailAddress]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola gerekli.")]
    [DataType(DataType.Password)]
    [Display(Name = "Parola")]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Restoran adı gerekli.")]
    [Display(Name = "Restoran adı")]
    [StringLength(120)]
    public string RestaurantName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şube adı gerekli.")]
    [Display(Name = "Şube adı")]
    [StringLength(120)]
    public string BranchName { get; set; } = "Ana şube";

    [Required(ErrorMessage = "E-posta gerekli.")]
    [EmailAddress]
    [Display(Name = "İş e-postası")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola gerekli.")]
    [DataType(DataType.Password)]
    [MinLength(12, ErrorMessage = "Parola en az 12 karakter olmalı.")]
    [Display(Name = "Parola")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola tekrarı gerekli.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Parolalar eşleşmiyor.")]
    [Display(Name = "Parola tekrar")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}

public sealed class SetupChecklistViewModel
{
    public string RestaurantName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string PlanCode { get; set; } = "Free";
    public string PlanDisplayName { get; set; } = "Free";
    public bool IsTrial { get; set; }
    public DateTimeOffset? TrialEndsAtUtc { get; set; }
    public int TableCount { get; set; }
    public int? MaxTablesPerBranch { get; set; }
    public int? MaxActiveQrCodes { get; set; }
    public int? MaxConcurrentLiveSessions { get; set; }
    public bool CanUseProductImages { get; set; }
    public bool CanUseMenuTranslations { get; set; }
    public bool CanUseLiveOrderPanel { get; set; }
    public List<string> EntitlementWarnings { get; set; } = [];
    public int ActiveQrCount { get; set; }
    public int MenuCount { get; set; }
    public int PublishedMenuCount { get; set; }
    public bool HasTables => TableCount > 0;
    public bool HasQr => ActiveQrCount > 0;
    public bool HasMenu => MenuCount > 0;
    public bool HasPublishedMenu => PublishedMenuCount > 0;
    public bool ReadyForGuests => HasTables && HasQr && HasPublishedMenu;
    public int CompletedSteps =>
        (HasTables ? 1 : 0) + (HasQr ? 1 : 0) + (HasMenu ? 1 : 0) + (HasPublishedMenu ? 1 : 0);
}

public sealed class WorkspaceViewModel
{
    public Guid TenantId { get; set; }
    public Guid RestaurantId { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public EntitlementUsageViewModel? Entitlements { get; set; }
    public List<string> Permissions { get; set; } = [];

    public bool HasPermission(string permission) =>
        Permissions.Contains(permission, StringComparer.Ordinal);

    public bool CanEditTables => HasPermission("Table.Edit");
    public bool CanEditMenus => HasPermission("Menu.Edit");
    public bool CanPublishMenus => HasPermission("Menu.Publish");
    public bool CanViewAnalytics => HasPermission("Analytics.View");
    public bool CanViewFinancials => HasPermission("Analytics.FinancialView");
    public bool CanManageBranches => HasPermission("Branch.Manage");
    public bool CanManageMembers => HasPermission("Branch.Members");
    public bool CanManageSubscription => HasPermission("Subscription.Manage");
    public bool CanAccessSettings => CanEditMenus || CanManageSubscription || CanManageMembers;
    public bool CanManagePromotions =>
        CanEditMenus && (Entitlements?.CanUsePromotions ?? true);
    /// <summary>Analytics page is open on Free; lookback is clamped by MaxOrderHistoryHours.</summary>
    public bool CanOpenAnalyticsPage =>
        CanViewAnalytics && (Entitlements?.CanUseAnalytics ?? true);
}

public sealed class EntitlementUsageViewModel
{
    public string PlanCode { get; set; } = "Free";
    public string PlanDisplayName { get; set; } = "Free";
    public int BranchCount { get; set; }
    public int? MaxBranches { get; set; }
    public int TableCount { get; set; }
    public int? MaxTablesPerBranch { get; set; }
    public int ActiveQrCount { get; set; }
    public int? MaxActiveQrCodes { get; set; }
    public int ActiveUserCount { get; set; }
    public int? MaxActiveUsers { get; set; }
    public int? MaxConcurrentLiveSessions { get; set; }
    public bool CanUseProductImages { get; set; }
    public bool CanUseMenuTranslations { get; set; }
    public bool CanManageAdditionalRoles { get; set; }
    public bool CanUseLiveOrderPanel { get; set; }
    public bool CanUsePromotions { get; set; }
    public bool CanUseAnalytics { get; set; }
    public bool CanUseMultiBranch { get; set; }
    public bool CanUseMenuThemes { get; set; }
    public bool CanUseBrandWatermark { get; set; }
    public bool HasPrioritySupport { get; set; }
    public bool IsTrial { get; set; }
    public DateTimeOffset? TrialEndsAtUtc { get; set; }
    public List<string> Warnings { get; set; } = [];
    public int IncludedBranches { get; set; }
    public int PurchasedBranchAddonCount { get; set; }
    public int FrozenBranchCount { get; set; }
    public int ActiveBranchCount { get; set; }
    public long ExtraBranchMonthlyPriceMinor { get; set; }
    public string BillingCurrency { get; set; } = "TRY";
    public bool NextBranchRequiresAddon { get; set; }
    /// <summary>Plan max lookback in hours (Free 72, Pro 720, Enterprise 2160).</summary>
    public int MaxOrderHistoryHours { get; set; } = 72;
}

public sealed class OrderListItemViewModel
{
    public Guid Id { get; set; }
    public string DisplayNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset StatusChangedAtUtc { get; set; }
    public DateTimeOffset EstimatedReadyAtUtc { get; set; }
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = "TRY";
    public Guid TableId { get; set; }
    public string TableLabel { get; set; } = string.Empty;
    public string ItemSummary { get; set; } = string.Empty;
}

public sealed class DashboardTodayViewModel
{
    public int TodaysOrderCount { get; set; }
    public long TodaysRevenueMinor { get; set; }
    public int OpenTablesCount { get; set; }
    public int PendingOrdersCount { get; set; }
    public int CompletedOrdersTodayCount { get; set; }
    public string Currency { get; set; } = "TRY";
    public DateTimeOffset DayStartUtc { get; set; }
    public DateTimeOffset DayEndUtc { get; set; }
    public bool CanViewFinancials { get; set; }
}

public sealed class SettingsViewModel
{
    public WorkspaceViewModel? Workspace { get; set; }
    public CustomerMenuSettingsViewModel CustomerMenu { get; set; } = new();
    public UpdateProfileViewModel Profile { get; set; } = new();
    public ChangePasswordViewModel ChangePassword { get; set; } = new();
}

public sealed class UpdateProfileViewModel
{
    [Required(ErrorMessage = "Ad soyad gerekli.")]
    [StringLength(120)]
    [Display(Name = "Ad Soyad")]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(40)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }
}

public sealed class ProfileApiModel
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
}

public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Mevcut şifre gerekli.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mevcut şifre")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni şifre gerekli.")]
    [StringLength(128, MinimumLength = 12)]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni şifre")]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class OrderLineViewModel
{
    public Guid Id { get; set; }
    public Guid MenuItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public long ListUnitPriceAmountMinor { get; set; }
    public long DiscountUnitAmountMinor { get; set; }
    public long UnitPriceAmountMinor { get; set; }
    public string Currency { get; set; } = "TRY";
    public string? Note { get; set; }

    public long LineTotalAmountMinor => UnitPriceAmountMinor * Quantity;
}

public sealed class OrderStatusHistoryViewModel
{
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset ChangedAtUtc { get; set; }
}

public sealed class OrderDetailViewModel
{
    public Guid Id { get; set; }
    public string DisplayNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string TableLabel { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset StatusChangedAtUtc { get; set; }
    public DateTimeOffset EstimatedReadyAtUtc { get; set; }
    public long SubtotalAmountMinor { get; set; }
    public long DiscountAmountMinor { get; set; }
    public long TotalAmountMinor { get; set; }
    public string Currency { get; set; } = "TRY";
    public List<OrderLineViewModel> Items { get; set; } = [];
    public List<OrderStatusHistoryViewModel> StatusHistory { get; set; } = [];
}

public sealed class ChangeOrderStatusViewModel
{
    public Guid OrderId { get; set; }
    public string DisplayNumber { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
    public DateTimeOffset ExpectedStatusChangedAtUtc { get; set; }
    public DateTimeOffset? CurrentEstimatedReadyAtUtc { get; set; }

    [Required]
    public string Status { get; set; } = string.Empty;

    /// <summary>Clock like 9:23 / 09:23, or minutes from now like 15.</summary>
    [Display(Name = "Tahmini hazır (isteğe bağlı)")]
    [MaxLength(8)]
    public string? EstimatedReadyInput { get; set; }
}

public sealed class TableListItemViewModel
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int ActiveQrCount { get; set; }
    public string OperationalStatus { get; set; } = "available";
}

public sealed class CreateTableViewModel
{
    [Required(ErrorMessage = "Masa etiketi gerekli.")]
    [Display(Name = "Masa etiketi")]
    public string Label { get; set; } = string.Empty;

    /// <summary>Shown when the plan table quota is already full (before submit).</summary>
    public string? LimitReachedMessage { get; set; }

    public bool IsLimitReached => !string.IsNullOrWhiteSpace(LimitReachedMessage);
}

public sealed class QrPrintViewModel
{
    public Guid Id { get; set; }
    public Guid TableId { get; set; }
    public string TableLabel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string EntryUrl { get; set; } = string.Empty;
    public string SvgMarkup { get; set; } = string.Empty;
}

public sealed class MenuSummaryViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Lifecycle { get; set; } = string.Empty;
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public int CategoryCount { get; set; }
    public int ItemCount { get; set; }
}

public sealed class ShareableMenuViewModel
{
    public Guid Id { get; set; }
    public Guid SourceBranchId { get; set; }
    public string SourceBranchName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int CategoryCount { get; set; }
    public int ItemCount { get; set; }
}

public sealed class MenusIndexViewModel
{
    public List<MenuSummaryViewModel> Menus { get; set; } = [];
    public List<ShareableMenuViewModel> ShareableMenus { get; set; } = [];
}

public sealed class CreateMenuViewModel
{
    [Required(ErrorMessage = "Menü adı gerekli.")]
    [Display(Name = "Menü adı")]
    public string Name { get; set; } = string.Empty;
}

public sealed class MenuDetailViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Lifecycle { get; set; } = string.Empty;
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public IReadOnlyList<MenuCategoryViewModel> Categories { get; set; } = [];
    public IReadOnlyList<MenuItemViewModel> Items { get; set; } = [];
}

public sealed class MenuEditorViewModel
{
    public MenuDetailViewModel Menu { get; set; } = new();
    public AddCategoryViewModel NewCategory { get; set; } = new();
    public AddMenuItemViewModel NewItem { get; set; } = new();
}

public sealed class AddCategoryViewModel
{
    public Guid MenuId { get; set; }

    [Required(ErrorMessage = "Kategori adı gerekli.")]
    [Display(Name = "Kategori adı")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Sıra")]
    [Range(0, 10_000)]
    public int SortOrder { get; set; }
}

public sealed class AddMenuItemViewModel
{
    public Guid MenuId { get; set; }

    [Required(ErrorMessage = "Kategori seçin.")]
    [Display(Name = "Kategori")]
    public Guid CategoryId { get; set; }

    [Required(ErrorMessage = "Ürün adı gerekli.")]
    [Display(Name = "Ürün adı")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Fiyat gerekli.")]
    [Display(Name = "Fiyat (₺)")]
    [Range(0.01, 1_000_000)]
    public decimal PriceTry { get; set; }

    [Display(Name = "Satışa açık")]
    public bool IsAvailable { get; set; } = true;

    [Display(Name = "Sıra")]
    [Range(0, 10_000)]
    public int SortOrder { get; set; }

    [Display(Name = "Fotoğraf URL")]
    [MaxLength(2048)]
    public string? ImageUrl { get; set; }

    [Display(Name = "Fotoğraf alt metni")]
    [MaxLength(200)]
    public string? ImageAlt { get; set; }

    [Display(Name = "Hazırlama (dk)")]
    [Range(0, 24 * 60)]
    public int? PrepMinutes { get; set; }

    [Display(Name = "Hazırlama (sn)")]
    [Range(0, 59)]
    public int? PrepSeconds { get; set; }

    [Display(Name = "Fotoğraf dosyası")]
    public IFormFile? Photo { get; set; }

    public MenuItemCatalogViewModel Catalog { get; set; } = new();
}

public sealed class EditCategoryViewModel
{
    public Guid Id { get; set; }
    public Guid MenuId { get; set; }

    [Required(ErrorMessage = "Kategori adı gerekli.")]
    [Display(Name = "Kategori adı")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Sıra")]
    [Range(0, 10_000)]
    public int SortOrder { get; set; }
}

public sealed class EditMenuItemViewModel
{
    public Guid Id { get; set; }
    public Guid MenuId { get; set; }

    [Required(ErrorMessage = "Kategori seçin.")]
    [Display(Name = "Kategori")]
    public Guid CategoryId { get; set; }

    [Required(ErrorMessage = "Ürün adı gerekli.")]
    [Display(Name = "Ürün adı")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Fiyat gerekli.")]
    [Display(Name = "Fiyat (₺)")]
    [Range(0.01, 1_000_000)]
    public decimal PriceTry { get; set; }

    [Display(Name = "Satışa açık")]
    public bool IsAvailable { get; set; } = true;

    [Display(Name = "Sıra")]
    [Range(0, 10_000)]
    public int SortOrder { get; set; }

    [Display(Name = "Fotoğraf URL")]
    [MaxLength(2048)]
    public string? ImageUrl { get; set; }

    [Display(Name = "Fotoğraf alt metni")]
    [MaxLength(200)]
    public string? ImageAlt { get; set; }

    [Display(Name = "Hazırlama (dk)")]
    [Range(0, 24 * 60)]
    public int? PrepMinutes { get; set; }

    [Display(Name = "Hazırlama (sn)")]
    [Range(0, 59)]
    public int? PrepSeconds { get; set; }

    [Display(Name = "Yeni fotoğraf")]
    public IFormFile? Photo { get; set; }

    public IReadOnlyList<MenuCategoryViewModel> Categories { get; set; } = [];
    public string? PreviewImageUrl { get; set; }
    public MenuItemCatalogViewModel Catalog { get; set; } = new();
}

public sealed class MenuCategoryViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class MenuItemViewModel
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = "TRY";
    public bool IsAvailable { get; set; }
    public int SortOrder { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ImageAlt { get; set; } = string.Empty;
    public string DisplayImageUrl { get; set; } = string.Empty;
    public int? PrepTimeSeconds { get; set; }
    public MenuItemCatalogApiModel? Catalog { get; set; }
}

public sealed class MenuItemImageFieldsViewModel
{
    public string FieldPrefix { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public string? ImageAlt { get; set; }

    public string? PreviewImageUrl { get; set; }

    public string NameInputId { get; set; } = "Name";
}

public sealed class StockPhotoLibraryViewModel
{
    public IReadOnlyList<StockPhotoCategoryViewModel> Categories { get; set; } = [];

    public IReadOnlyList<StockPhotoViewModel> Photos { get; set; } = [];
}

public sealed class StockPhotoCategoryViewModel
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}

public sealed class StockPhotoViewModel
{
    public string Id { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Alt { get; set; } = string.Empty;

    public string[] Tags { get; set; } = [];
}

public sealed class AnalyticsDashboardViewModel
{
    public int Days { get; set; } = 3;
    public int MaxDays { get; set; } = 3;
    public bool WasClamped { get; set; }
    public bool CanViewFinancials { get; set; }
    public bool CanManageSubscription { get; set; }
    public bool IsTrial { get; set; }
    public string PlanDisplayName { get; set; } = "Free";
    public UpgradePromptViewModel? UpgradePrompt { get; set; }
    public AnalyticsSummaryViewModel? Summary { get; set; }
    public List<AnalyticsPeriodViewModel> Periods { get; set; } = [];
    public List<AnalyticsTopItemViewModel> TopItems { get; set; } = [];
}

public sealed class AnalyticsSummaryViewModel
{
    public long GrossSalesMinor { get; set; }
    public long? EstimatedCostMinor { get; set; }
    public long? GrossProfitMinor { get; set; }
    public long CancelledSalesMinor { get; set; }
    public int CompletedOrderCount { get; set; }
    public int CancelledOrderCount { get; set; }
    public int OpenServiceRequestCount { get; set; }
    public long AverageTicketMinor { get; set; }
    public string Currency { get; set; } = "TRY";
    public bool CanViewFinancials { get; set; }
}

public sealed class AnalyticsPeriodViewModel
{
    public DateTimeOffset PeriodStartUtc { get; set; }
    public long GrossSalesMinor { get; set; }
    public long? GrossProfitMinor { get; set; }
    public int OrderCount { get; set; }
}

public sealed class AnalyticsTopItemViewModel
{
    public Guid MenuItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
    public long RevenueMinor { get; set; }
    public long? EstimatedCostMinor { get; set; }
    public long? GrossProfitMinor { get; set; }
}

public sealed class MenuPromotionListItemViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Scope { get; set; } = "all_menu";
    public string DiscountKind { get; set; } = "percent";
    public int DiscountValue { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public TimeOnly? DailyStartLocal { get; set; }
    public TimeOnly? DailyEndLocal { get; set; }
    public byte? DaysOfWeekMask { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? MenuItemId { get; set; }
    public bool IsActive { get; set; }
}

public sealed class PromotionsPageViewModel
{
    public IReadOnlyList<MenuPromotionListItemViewModel> Promotions { get; set; } = [];
    public CreateMenuPromotionViewModel Create { get; set; } = new();
    public MenuDetailViewModel? PublishedMenu { get; set; }
}

public sealed class CreateMenuPromotionViewModel
{
    [Required(ErrorMessage = "Kampanya adı gerekli.")]
    [Display(Name = "Kampanya adı")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Kapsam")]
    public string Scope { get; set; } = "all_menu";

    [Display(Name = "İndirim türü")]
    public string DiscountKind { get; set; } = "percent";

    [Display(Name = "İndirim değeri")]
    public string DiscountValue { get; set; } = "20";

    [Display(Name = "Günlük başlangıç")]
    public string? DailyStartLocal { get; set; }

    [Display(Name = "Günlük bitiş")]
    public string? DailyEndLocal { get; set; }

    /// <summary>DayOfWeek bits (0=Pazar … 6=Cumartesi). Empty/all = every day.</summary>
    public int[]? WeekdayBits { get; set; } = [1, 2, 3, 4, 5, 6, 0];

    [Display(Name = "Kategori")]
    public Guid? CategoryId { get; set; }

    [Display(Name = "Ürün")]
    public Guid? MenuItemId { get; set; }

    [Display(Name = "Bitiş tarihi (isteğe bağlı)")]
    public DateTime? EndsAtLocal { get; set; }
}

public sealed class LunchPackageComponentViewModel
{
    public Guid Id { get; set; }
    public Guid MenuItemId { get; set; }
    public string MenuItemName { get; set; } = string.Empty;
    public string? SlotLabel { get; set; }
    public int SortOrder { get; set; }
    public long ListAmountMinor { get; set; }
}

public sealed class LunchPackageListItemViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long PriceAmountMinor { get; set; }
    public string Currency { get; set; } = "TRY";
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public string? DailyStartLocal { get; set; }
    public string? DailyEndLocal { get; set; }
    public byte? DaysOfWeekMask { get; set; }
    public long ComponentsListTotalMinor { get; set; }
    public IReadOnlyList<LunchPackageComponentViewModel> Components { get; set; } = [];
}

public sealed class LunchPackagesPageViewModel
{
    public IReadOnlyList<LunchPackageListItemViewModel> Packages { get; set; } = [];
    public CreateLunchPackageViewModel Create { get; set; } = new();
    public MenuDetailViewModel? PublishedMenu { get; set; }
}

public sealed class CreateLunchPackageViewModel
{
    [Required(ErrorMessage = "Menü adı gerekli.")]
    [Display(Name = "Menü adı")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Menü fiyatı gerekli.")]
    [Display(Name = "Menü fiyatı (₺)")]
    public string PriceLira { get; set; } = string.Empty;

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Sıra")]
    public int SortOrder { get; set; }

    [Display(Name = "Günlük başlangıç")]
    public string? DailyStartLocal { get; set; } = "11:30";

    [Display(Name = "Günlük bitiş")]
    public string? DailyEndLocal { get; set; } = "15:00";

    public int[]? WeekdayBits { get; set; } = [1, 2, 3, 4, 5];

    [Display(Name = "Ürünler")]
    public Guid[]? ComponentMenuItemIds { get; set; }

    public string?[]? ComponentSlotLabels { get; set; }
}

public sealed class ManagedNotificationListItemViewModel
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string Audience { get; set; } = "all";
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset? LastDispatchedAtUtc { get; set; }
}

public sealed class NotificationsPageViewModel
{
    public IReadOnlyList<ManagedNotificationListItemViewModel> Notifications { get; set; } = [];
    public CreateNotificationViewModel Create { get; set; } = new();
    public string? LastDispatchSummary { get; set; }
}

public sealed class CreateNotificationViewModel
{
    [Display(Name = "Hedef kitle")]
    public string Audience { get; set; } = "non_pro";

    [Required(ErrorMessage = "Başlık gerekli.")]
    [Display(Name = "Başlık")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mesaj gerekli.")]
    [Display(Name = "Mesaj")]
    public string Body { get; set; } = string.Empty;

    [Display(Name = "Bağlantı (isteğe bağlı)")]
    public string? ActionUrl { get; set; }

    [Display(Name = "Tüm restoranlara yayınla (platform)")]
    public bool BroadcastToAllTenants { get; set; }
}

public sealed class NotificationDispatchResultViewModel
{
    public int EmailSentCount { get; set; }
    public int PushSentCount { get; set; }
    public IReadOnlyList<string> RecipientEmails { get; set; } = [];
}

public sealed class AdminLoginViewModel
{
    [Required(ErrorMessage = "E-posta gerekli.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola gerekli.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}

public sealed class AdminNotificationsPageViewModel
{
    public IReadOnlyList<ManagedNotificationListItemViewModel> Notifications { get; set; } = [];
    public CreateAdminNotificationViewModel Create { get; set; } = new();
    public string? LastDispatchSummary { get; set; }
}

public sealed class CreateAdminNotificationViewModel
{
    [Display(Name = "Hedef kitle")]
    public string Audience { get; set; } = "non_pro";

    [Required(ErrorMessage = "Başlık gerekli.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mesaj gerekli.")]
    public string Body { get; set; } = string.Empty;

    public string? ActionUrl { get; set; }
}

public sealed class AdminSubscriptionOffersPageViewModel
{
    public IReadOnlyList<AdminSubscriptionOfferListItemViewModel> Offers { get; set; } = [];
    public CreateAdminSubscriptionOfferViewModel Create { get; set; } = new();
}

public sealed class AdminSubscriptionOfferListItemViewModel
{
    public Guid Id { get; set; }
    public string Audience { get; set; } = string.Empty;
    public string TargetPlanCode { get; set; } = string.Empty;
    public int DiscountPercent { get; set; }
    public int DurationMonths { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset? EndsAtUtc { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateAdminSubscriptionOfferViewModel
{
    [Display(Name = "Hedef kitle")]
    public string Audience { get; set; } = "non_pro";

    [Display(Name = "Hedef plan")]
    public string TargetPlanCode { get; set; } = "pro";

    [Display(Name = "İndirim (%)")]
    public int DiscountPercent { get; set; } = 20;

    [Display(Name = "Süre (ay)")]
    public int DurationMonths { get; set; } = 3;

    [Required(ErrorMessage = "Kampanya başlığı gerekli.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Açıklama gerekli.")]
    public string Body { get; set; } = string.Empty;

    [Display(Name = "Bitiş tarihi")]
    [DataType(DataType.Date)]
    public DateTime? EndsAtLocal { get; set; }
}

public sealed class BranchesPageViewModel
{
    public WorkspaceViewModel? Workspace { get; set; }
    public bool CanUseMultiBranch { get; set; }
    public bool CanManageBranches { get; set; }
    public bool CanManageMembers { get; set; }
    public int? MaxBranches { get; set; }
    public BranchBillingPreviewViewModel? Billing { get; set; }
    public IReadOnlyList<BranchListItemViewModel> Branches { get; set; } = [];
    public IReadOnlyList<MembershipScopeViewModel> Memberships { get; set; } = [];
    public NetworkSummaryViewModel? Network { get; set; }
    public Guid? SelectedBranchId { get; set; }
    public IReadOnlyList<BranchMemberViewModel> SelectedMembers { get; set; } = [];
    public CreateBranchViewModel CreateBranch { get; set; } = new();
    public InviteBranchMemberViewModel InviteMember { get; set; } = new();
    public EnterpriseQuoteViewModel EnterpriseQuote { get; set; } = new();
}

public sealed class BranchBillingPreviewViewModel
{
    public string PlanCode { get; set; } = string.Empty;
    public string PlanDisplayName { get; set; } = string.Empty;
    public bool IsTrial { get; set; }
    public DateTimeOffset? TrialEndsAtUtc { get; set; }
    public int IncludedBranches { get; set; }
    public int PurchasedBranchAddonCount { get; set; }
    public int? MaxBranches { get; set; }
    public int ActiveBranchCount { get; set; }
    public int FrozenBranchCount { get; set; }
    public bool NextBranchRequiresAddon { get; set; }
    public long ExtraBranchMonthlyPriceMinor { get; set; }
    public string Currency { get; set; } = "TRY";
    public bool CanUseMultiBranch { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public sealed class BranchListItemViewModel
{
    public Guid Id { get; set; }
    public Guid RestaurantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ActiveMemberCount { get; set; }
    public int ActiveTableCount { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsFrozen { get; set; }
}

public sealed class MembershipScopeViewModel
{
    public Guid MembershipId { get; set; }
    public Guid TenantId { get; set; }
    public Guid RestaurantId { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool CanManageBranches { get; set; }
    public bool CanManageMembers { get; set; }
}

public sealed class BranchMemberViewModel
{
    public Guid MembershipId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string RoleKey { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset? LastLoginAtUtc { get; set; }
}

public sealed class NetworkSummaryViewModel
{
    public long GrossSalesMinor { get; set; }
    public int CompletedOrderCount { get; set; }
    public int CancelledOrderCount { get; set; }
    public int OpenOrderCount { get; set; }
    public int OpenServiceRequestCount { get; set; }
    public int BranchCount { get; set; }
    public string Currency { get; set; } = "TRY";
    public IReadOnlyList<NetworkBranchStatViewModel> Branches { get; set; } = [];
}

public sealed class NetworkBranchStatViewModel
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public long GrossSalesMinor { get; set; }
    public int CompletedOrderCount { get; set; }
    public int CancelledOrderCount { get; set; }
    public int OpenOrderCount { get; set; }
    public int OpenServiceRequestCount { get; set; }
    public int ActiveTableCount { get; set; }
    public int ActiveMemberCount { get; set; }
    public long AverageTicketMinor { get; set; }
}

public sealed class CreateBranchViewModel
{
    [Required(ErrorMessage = "Şube adı gerekli.")]
    [StringLength(120)]
    [Display(Name = "Yeni şube adı")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Ek şube ücretini onaylıyorum")]
    public bool ConfirmAddonPurchase { get; set; }
}

public sealed class InviteBranchMemberViewModel
{
    public Guid BranchId { get; set; }

    [Required(ErrorMessage = "E-posta gerekli.")]
    [EmailAddress]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ad soyad gerekli.")]
    [StringLength(120)]
    [Display(Name = "Ad Soyad")]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(40)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [StringLength(128)]
    [Display(Name = "Şifre (yeni hesapsa)")]
    public string? Password { get; set; }

    [Display(Name = "Rol")]
    public string RoleKey { get; set; } = "manager";
}

public sealed class UpdateBranchMemberViewModel
{
    public Guid BranchId { get; set; }
    public Guid MembershipId { get; set; }

    [Required(ErrorMessage = "Ad soyad gerekli.")]
    [StringLength(120)]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(40)]
    public string? Phone { get; set; }

    public string? RoleKey { get; set; }
}

public sealed class ResetBranchMemberPasswordViewModel
{
    public Guid BranchId { get; set; }
    public Guid MembershipId { get; set; }

    [Required(ErrorMessage = "Şifre gerekli.")]
    [StringLength(128, MinimumLength = 12)]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class RenameBranchViewModel
{
    public Guid BranchId { get; set; }

    [Required(ErrorMessage = "Şube adı gerekli.")]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;
}

public sealed class EnterpriseQuoteViewModel
{
    [Required(ErrorMessage = "İsim gerekli.")]
    [StringLength(120)]
    [Display(Name = "Yetkili adı")]
    public string ContactName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta gerekli.")]
    [EmailAddress]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [StringLength(40)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [Range(3, 500)]
    [Display(Name = "Tahmini şube sayısı")]
    public int EstimatedBranchCount { get; set; } = 5;

    [StringLength(2000)]
    [Display(Name = "Not")]
    public string? Note { get; set; }
}

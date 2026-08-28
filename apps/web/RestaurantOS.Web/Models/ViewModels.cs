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
    public int? MaxTablesPerBranch { get; set; } = 8;
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
}

public sealed class EntitlementUsageViewModel
{
    public string PlanCode { get; set; } = "Free";
    public string PlanDisplayName { get; set; } = "Free";
    public int BranchCount { get; set; }
    public int? MaxBranches { get; set; }
    public int TableCount { get; set; }
    public int? MaxTablesPerBranch { get; set; }
    public int ActiveUserCount { get; set; }
    public int? MaxActiveUsers { get; set; }
    public bool CanUseProductImages { get; set; }
    public bool CanUseMenuTranslations { get; set; }
    public bool CanManageAdditionalRoles { get; set; }
    public bool CanUseLiveOrderPanel { get; set; }
    public bool CanUseMultiBranch { get; set; }
    public bool HasPrioritySupport { get; set; }
    public bool IsTrial { get; set; }
    public DateTimeOffset? TrialEndsAtUtc { get; set; }
    public List<string> Warnings { get; set; } = [];
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
}

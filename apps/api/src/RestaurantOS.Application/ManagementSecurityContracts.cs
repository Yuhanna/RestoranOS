using RestaurantOS.Domain;

namespace RestaurantOS.Application;

public interface IManagementAuthService
{
    Task<ManagementTokenResult> RegisterAsync(
        string email,
        string password,
        string restaurantName,
        string branchName,
        CancellationToken cancellationToken);

    Task<ManagementTokenResult> LoginAsync(
        string email,
        string password,
        Guid? tenantId,
        Guid? branchId,
        CancellationToken cancellationToken);

    Task<ManagementTokenResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
}

public interface IManagementOrderService
{
    Task<IReadOnlyList<ManagementOrderResult>> GetActiveOrdersAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken);

    Task<CustomerOrderResult> ChangeOrderStatusAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid orderId,
        string status,
        DateTimeOffset expectedStatusChangedAtUtc,
        CancellationToken cancellationToken,
        DateTimeOffset? estimatedReadyAtUtc = null);
}

public interface IManagementTableService
{
    Task<IReadOnlyList<ManagementTableResult>> ListTablesAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken);

    Task<ManagementTableResult> CreateTableAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        string label,
        CancellationToken cancellationToken);

    Task<ManagementTableResult> UpdateTableAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        string? label,
        bool? isActive,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ManagementQrCodeResult>> ListQrCodesAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        CancellationToken cancellationToken);

    Task<ManagementGeneratedQrResult> GenerateQrAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        CancellationToken cancellationToken);

    Task<ManagementQrCodeResult> ChangeQrStatusAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid qrCodeId,
        QrCodeStatus nextStatus,
        CancellationToken cancellationToken);

    Task<ManagementQrPrintResult> GetPrintPayloadAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid qrCodeId,
        CancellationToken cancellationToken);
}

public sealed record ManagementTableResult(Guid Id, string Label, bool IsActive, int ActiveQrCount);

public sealed record ManagementQrCodeResult(
    Guid Id,
    Guid TableId,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RevokedAtUtc);

public sealed record ManagementGeneratedQrResult(
    Guid Id,
    Guid TableId,
    string TableLabel,
    string Status,
    string Token,
    string EntryUrl,
    string SvgMarkup,
    DateTimeOffset CreatedAtUtc);

public sealed record ManagementQrPrintResult(
    Guid Id,
    Guid TableId,
    string TableLabel,
    string Status,
    string EntryUrl,
    string SvgMarkup);

public interface IManagementMenuService
{
    Task<IReadOnlyList<ManagementMenuSummaryResult>> ListMenusAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken);

    Task<ManagementMenuDetailResult> GetMenuAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid menuId,
        CancellationToken cancellationToken);

    Task<ManagementMenuSummaryResult> CreateMenuAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        string name,
        CancellationToken cancellationToken);

    Task<ManagementMenuSummaryResult> RenameMenuAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid menuId,
        string name,
        CancellationToken cancellationToken);

    Task<ManagementMenuSummaryResult> PublishMenuAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid menuId,
        CancellationToken cancellationToken);

    Task<ManagementMenuSummaryResult> UnpublishMenuAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid menuId,
        CancellationToken cancellationToken);

    Task<ManagementMenuSummaryResult> ArchiveMenuAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid menuId,
        CancellationToken cancellationToken);

    Task<ManagementMenuCategoryResult> AddCategoryAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid menuId,
        string name,
        int sortOrder,
        CancellationToken cancellationToken);

    Task<ManagementMenuCategoryResult> UpdateCategoryAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid categoryId,
        string? name,
        int? sortOrder,
        CancellationToken cancellationToken);

    Task<ManagementMenuItemResult> AddItemAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid menuId,
        Guid categoryId,
        string name,
        string description,
        long amountMinor,
        bool isAvailable,
        int sortOrder,
        string? imageUrl,
        string? imageAlt,
        CancellationToken cancellationToken,
        int? prepTimeSeconds = null);

    Task<ManagementMenuItemResult> UpdateItemAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid itemId,
        Guid? categoryId,
        string? name,
        string? description,
        long? amountMinor,
        bool? isAvailable,
        int? sortOrder,
        string? imageUrl,
        string? imageAlt,
        CancellationToken cancellationToken,
        int? prepTimeSeconds = null,
        bool updatePrepTime = false);

    Task<ManagementMenuItemResult> SetItemImageAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid itemId,
        string imageUrl,
        string? imageAlt,
        CancellationToken cancellationToken);

    Task<MenuTextTranslationResult> UpsertCategoryTranslationAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid categoryId,
        string locale,
        string name,
        CancellationToken cancellationToken);

    Task<MenuTextTranslationResult> UpsertItemTranslationAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid itemId,
        string locale,
        string name,
        string description,
        CancellationToken cancellationToken);
}

public sealed record ManagementMenuSummaryResult(
    Guid Id,
    string Name,
    string Lifecycle,
    DateTimeOffset? PublishedAtUtc,
    int CategoryCount,
    int ItemCount);

public sealed record ManagementMenuDetailResult(
    Guid Id,
    string Name,
    string Lifecycle,
    DateTimeOffset? PublishedAtUtc,
    IReadOnlyList<ManagementMenuCategoryResult> Categories,
    IReadOnlyList<ManagementMenuItemResult> Items);

public sealed record MenuTextTranslationResult(string Locale, string Name, string Description);

public sealed record ManagementMenuCategoryResult(
    Guid Id,
    Guid MenuId,
    string Name,
    int SortOrder,
    IReadOnlyList<MenuTextTranslationResult> Translations);

public sealed record ManagementMenuItemResult(
    Guid Id,
    Guid MenuId,
    Guid CategoryId,
    string Name,
    string Description,
    long AmountMinor,
    string Currency,
    bool IsAvailable,
    int SortOrder,
    string ImageUrl,
    string ImageAlt,
    IReadOnlyList<MenuTextTranslationResult> Translations,
    int? PrepTimeSeconds = null);

public sealed class CustomerWebOptions
{
    public const string SectionName = "CustomerWeb";
    public string PublicBaseUrl { get; set; } = "http://localhost:5173";
}

public interface IManagementOrderNotifier
{
    Task NotifyAsync(
        Guid tenantId,
        Guid branchId,
        CustomerOrderResult order,
        CancellationToken cancellationToken);

    Task NotifyServiceRequestAsync(
        Guid tenantId,
        Guid branchId,
        ServiceRequestResult request,
        CancellationToken cancellationToken);
}

public interface IManagementServiceRequestService
{
    Task<IReadOnlyList<ServiceRequestResult>> ListOpenAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken);

    Task<ServiceRequestResult> CompleteAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid requestId,
        CancellationToken cancellationToken);
}

public sealed record ManagementTokenResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    Guid UserId,
    Guid TenantId,
    Guid BranchId);

public sealed record ManagementOrderResult(
    Guid Id,
    string DisplayNumber,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset StatusChangedAtUtc,
    DateTimeOffset EstimatedReadyAtUtc,
    long AmountMinor,
    string Currency);

public sealed class ManagementAuthException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class EntitlementException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public interface IFeatureEntitlementService
{
    Task<FeatureEntitlements> GetEntitlementsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<TenantEntitlementUsageResult> GetUsageAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken);

    Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken);

    Task EnsureCanCreateTableAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken);

    Task EnsureCanAddUserAsync(Guid tenantId, CancellationToken cancellationToken);

    Task EnsureCanUseProductImagesAsync(Guid tenantId, CancellationToken cancellationToken);

    Task EnsureCanUseMenuTranslationsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task EnsureCanManageAdditionalRolesAsync(Guid tenantId, CancellationToken cancellationToken);

    Task EnsureTenantSubscriptionAsync(
        Guid tenantId,
        string planCode,
        CancellationToken cancellationToken);
}

public sealed record TenantEntitlementUsageResult(
    string PlanCode,
    string PlanDisplayName,
    int BranchCount,
    int? MaxBranches,
    int TableCount,
    int? MaxTablesPerBranch,
    int ActiveUserCount,
    int? MaxActiveUsers,
    bool CanUseProductImages,
    bool CanUseMenuTranslations,
    bool CanManageAdditionalRoles,
    bool CanUseLiveOrderPanel,
    bool CanUseMultiBranch,
    bool HasPrioritySupport,
    IReadOnlyList<string> Warnings,
    bool IsTrial = false,
    DateTimeOffset? TrialEndsAtUtc = null);


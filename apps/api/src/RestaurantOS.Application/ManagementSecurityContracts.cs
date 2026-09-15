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

    Task<ManagementTokenResult> LoginPlatformAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<ManagementTokenResult> RefreshAsync(
        string refreshToken,
        string expectedRealm,
        CancellationToken cancellationToken);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);

    Task<ManagementTokenResult> SwitchBranchAsync(
        Guid userId,
        Guid tenantId,
        Guid targetBranchId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ManagementMembershipScopeResult>> ListMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken);
}

public interface IManagementOrderService
{
    Task<IReadOnlyList<ManagementOrderResult>> GetActiveOrdersAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken);

    Task<ManagementOrderDetailResult?> GetOrderByIdAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid orderId,
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

    Task<IReadOnlyList<ManagementOrderResult>> GetHistoryOrdersAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        int hours,
        int take,
        CancellationToken cancellationToken);
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

    Task<ManagementTableResult> ReleaseTableAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        CancellationToken cancellationToken);

    Task<ManagementTableCheckResult> GetTableCheckAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        CancellationToken cancellationToken);

    Task<ManagementTableCheckCloseResult> CloseTableCheckAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        string tender,
        bool confirmIncompleteKitchen,
        string? note,
        CancellationToken cancellationToken);
}

public sealed record ManagementTableCheckResult(
    int RoundCount,
    long TotalAmountMinor,
    bool HasIncompleteKitchen);

public sealed record ManagementTableCheckCloseResult(
    int ClosedOrderCount,
    long TotalAmountMinor,
    string Tender,
    bool ForcedIncompleteKitchen);

public sealed record ManagementTableResult(
    Guid Id,
    string Label,
    bool IsActive,
    int ActiveQrCount,
    string OperationalStatus);

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

    /// <summary>
    /// Lists published menus from other branches of the same restaurant (for copy).
    /// </summary>
    Task<IReadOnlyList<ManagementShareableMenuResult>> ListShareableMenusAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deep-copies a published menu from another branch in the same restaurant into this branch as a draft.
    /// </summary>
    Task<ManagementMenuSummaryResult> CloneMenuAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid sourceMenuId,
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
        int? prepTimeSeconds = null,
        MenuItemCatalogData? catalog = null);

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
        bool updatePrepTime = false,
        MenuItemCatalogData? catalog = null,
        bool updateCatalog = false);

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

public sealed record ManagementShareableMenuResult(
    Guid Id,
    Guid SourceBranchId,
    string SourceBranchName,
    string Name,
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
    int? PrepTimeSeconds = null,
    MenuItemCatalogData Catalog = default!);

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
    Guid BranchId,
    string Realm = AuthRealms.Management,
    string? RoleCode = null,
    string? Email = null);

public sealed record ManagementMembershipScopeResult(
    Guid MembershipId,
    Guid TenantId,
    Guid RestaurantId,
    string RestaurantName,
    Guid BranchId,
    string BranchName,
    string RoleName,
    bool CanManageBranches);

public interface IManagementBranchService
{
    Task<IReadOnlyList<ManagementBranchResult>> ListBranchesAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<ManagementBranchResult> CreateBranchAsync(
        Guid actorUserId,
        Guid tenantId,
        Guid currentBranchId,
        string name,
        bool confirmAddonPurchase,
        CancellationToken cancellationToken);

    Task<ManagementBranchResult> RenameBranchAsync(
        Guid tenantId,
        Guid branchId,
        string name,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ManagementBranchMemberResult>> ListMembersAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken);

    Task<ManagementBranchMemberResult> InviteMemberAsync(
        Guid actorUserId,
        Guid tenantId,
        Guid branchId,
        string email,
        string? password,
        string roleKey,
        CancellationToken cancellationToken);

    Task DeactivateMemberAsync(
        Guid actorUserId,
        Guid tenantId,
        Guid branchId,
        Guid membershipId,
        CancellationToken cancellationToken);

    Task<ManagementNetworkSummaryResult> GetNetworkSummaryAsync(
        Guid tenantId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);
}

public sealed record ManagementBranchResult(
    Guid Id,
    Guid RestaurantId,
    string Name,
    int ActiveMemberCount,
    int ActiveTableCount,
    bool IsCurrent,
    bool IsFrozen = false);

public sealed record ManagementBranchMemberResult(
    Guid MembershipId,
    Guid UserId,
    string Email,
    string RoleName,
    string RoleKey,
    bool IsActive,
    DateTimeOffset? LastLoginAtUtc);

public sealed record ManagementNetworkBranchStatResult(
    Guid BranchId,
    string BranchName,
    long GrossSalesMinor,
    int CompletedOrderCount,
    int CancelledOrderCount,
    int OpenOrderCount,
    int OpenServiceRequestCount,
    int ActiveTableCount,
    int ActiveMemberCount,
    long AverageTicketMinor);

public sealed record ManagementNetworkSummaryResult(
    long GrossSalesMinor,
    int CompletedOrderCount,
    int CancelledOrderCount,
    int OpenOrderCount,
    int OpenServiceRequestCount,
    int BranchCount,
    string Currency,
    IReadOnlyList<ManagementNetworkBranchStatResult> Branches);

public sealed record ManagementOrderResult(
    Guid Id,
    string DisplayNumber,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset StatusChangedAtUtc,
    DateTimeOffset EstimatedReadyAtUtc,
    long AmountMinor,
    string Currency,
    Guid TableId,
    string TableLabel);

public sealed record ManagementOrderLineResult(
    Guid Id,
    Guid MenuItemId,
    string Name,
    int Quantity,
    long ListUnitPriceAmountMinor,
    long DiscountUnitAmountMinor,
    long UnitPriceAmountMinor,
    string Currency,
    string? Note);

public sealed record ManagementOrderStatusHistoryEntry(
    string Status,
    DateTimeOffset ChangedAtUtc,
    Guid? ChangedByUserId);

public sealed record ManagementOrderDetailResult(
    Guid Id,
    string DisplayNumber,
    string Status,
    Guid TableId,
    string TableLabel,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset StatusChangedAtUtc,
    DateTimeOffset EstimatedReadyAtUtc,
    long SubtotalAmountMinor,
    long DiscountAmountMinor,
    long TotalAmountMinor,
    string Currency,
    IReadOnlyList<ManagementOrderLineResult> Items,
    IReadOnlyList<ManagementOrderStatusHistoryEntry> StatusHistory);

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

    Task EnsureCanCreateBranchAsync(
        Guid tenantId,
        bool confirmAddonPurchase,
        CancellationToken cancellationToken);

    Task EnsureBranchNotFrozenAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken);

    Task EnsureCanCreateTableAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken);

    Task EnsureCanAddUserAsync(Guid tenantId, CancellationToken cancellationToken);

    Task EnsureCanUseProductImagesAsync(Guid tenantId, CancellationToken cancellationToken);

    Task EnsureCanUseMenuTranslationsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task EnsureCanManageAdditionalRolesAsync(Guid tenantId, CancellationToken cancellationToken);

    Task EnsureTenantSubscriptionAsync(
        Guid tenantId,
        string planCode,
        CancellationToken cancellationToken);

    Task<TenantEntitlementUsageResult> ConvertToPaidPlanAsync(
        Guid tenantId,
        Guid branchId,
        string planCode,
        CancellationToken cancellationToken);

    Task<EnterpriseQuoteRequestResult> RequestEnterpriseQuoteAsync(
        Guid tenantId,
        Guid userId,
        string contactName,
        string email,
        string? phone,
        int estimatedBranchCount,
        string? note,
        CancellationToken cancellationToken);

    Task<ManagementBranchBillingPreviewResult> GetBranchBillingPreviewAsync(
        Guid tenantId,
        CancellationToken cancellationToken);
}

public interface IManagementDashboardService
{
    Task<ManagementTodayDashboardResult> GetTodayAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken);
}

public sealed record ManagementTodayDashboardResult(
    int TodaysOrderCount,
    long TodaysRevenueMinor,
    int OpenTablesCount,
    int PendingOrdersCount,
    int CompletedOrdersTodayCount,
    string Currency,
    DateTimeOffset DayStartUtc,
    DateTimeOffset DayEndUtc);

public interface IManagementAnalyticsService
{
    Task<ManagementAnalyticsSummaryResult> GetSummaryAsync(
        Guid tenantId,
        Guid branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ManagementSalesPeriodResult>> GetSalesByPeriodAsync(
        Guid tenantId,
        Guid branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string granularity,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ManagementTopItemResult>> GetTopItemsAsync(
        Guid tenantId,
        Guid branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int limit,
        CancellationToken cancellationToken);
}

public sealed record ManagementAnalyticsSummaryResult(
    long GrossSalesMinor,
    long EstimatedCostMinor,
    long GrossProfitMinor,
    long CancelledSalesMinor,
    int CompletedOrderCount,
    int CancelledOrderCount,
    int OpenServiceRequestCount,
    long AverageTicketMinor,
    string Currency);

public sealed record ManagementSalesPeriodResult(
    DateTimeOffset PeriodStartUtc,
    long GrossSalesMinor,
    long GrossProfitMinor,
    int OrderCount);

public sealed record ManagementTopItemResult(
    Guid MenuItemId,
    string Name,
    int QuantitySold,
    long RevenueMinor,
    long EstimatedCostMinor,
    long GrossProfitMinor);

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
    DateTimeOffset? TrialEndsAtUtc = null,
    IReadOnlyList<TenantAudienceNotificationResult>? Notifications = null,
    IReadOnlyList<SubscriptionOfferResult>? SubscriptionOffers = null,
    int IncludedBranches = 0,
    int PurchasedBranchAddonCount = 0,
    int FrozenBranchCount = 0,
    int ActiveBranchCount = 0,
    long ExtraBranchMonthlyPriceMinor = 0,
    string BillingCurrency = "TRY",
    bool NextBranchRequiresAddon = false);

public sealed record EnterpriseQuoteRequestResult(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    string Message);

public sealed record ManagementBranchBillingPreviewResult(
    string PlanCode,
    string PlanDisplayName,
    bool IsTrial,
    DateTimeOffset? TrialEndsAtUtc,
    int IncludedBranches,
    int PurchasedBranchAddonCount,
    int? MaxBranches,
    int ActiveBranchCount,
    int FrozenBranchCount,
    bool NextBranchRequiresAddon,
    long ExtraBranchMonthlyPriceMinor,
    string Currency,
    bool CanUseMultiBranch,
    string Summary);

public sealed record TenantAudienceNotificationResult(
    Guid Id,
    string Title,
    string Body,
    string? ActionUrl);

public sealed record SubscriptionOfferResult(
    Guid Id,
    string TargetPlanCode,
    int DiscountPercent,
    int DurationMonths,
    string Title,
    string Body);

public interface IPromotionManagementService
{
    Task<IReadOnlyList<MenuPromotionResult>> ListMenuPromotionsAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken);

    Task<MenuPromotionResult> CreateMenuPromotionAsync(
        Guid tenantId,
        Guid branchId,
        CreateMenuPromotionCommand command,
        CancellationToken cancellationToken);

    Task<MenuPromotionResult> UpdateMenuPromotionAsync(
        Guid tenantId,
        Guid branchId,
        Guid promotionId,
        UpdateMenuPromotionCommand command,
        CancellationToken cancellationToken);
}

public sealed record MenuPromotionResult(
    Guid Id,
    string Name,
    string Scope,
    string DiscountKind,
    int DiscountValue,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    TimeOnly? DailyStartLocal,
    TimeOnly? DailyEndLocal,
    Guid? CategoryId,
    Guid? MenuItemId,
    bool IsActive);

public sealed record CreateMenuPromotionCommand(
    string Name,
    string Scope,
    string DiscountKind,
    int DiscountValue,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    TimeOnly? DailyStartLocal,
    TimeOnly? DailyEndLocal,
    Guid? CategoryId,
    Guid? MenuItemId,
    bool IsActive = true);

public sealed record UpdateMenuPromotionCommand(
    string? Name,
    bool? IsActive,
    DateTimeOffset? EndsAtUtc);

public interface INotificationManagementService
{
    Task<IReadOnlyList<ManagedNotificationResult>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<ManagedNotificationResult> CreateAsync(
        Guid tenantId,
        CreateManagedNotificationCommand command,
        CancellationToken cancellationToken);

    Task<NotificationDispatchResult> DispatchAsync(
        Guid tenantId,
        Guid notificationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ManagedNotificationResult>> ListPlatformAsync(
        CancellationToken cancellationToken);

    Task<ManagedNotificationResult> CreatePlatformAsync(
        CreatePlatformNotificationCommand command,
        CancellationToken cancellationToken);

    Task<NotificationDispatchResult> DispatchPlatformAsync(
        Guid notificationId,
        CancellationToken cancellationToken);
}

public sealed record CreatePlatformNotificationCommand(
    string Audience,
    string Title,
    string Body,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    string? ActionUrl,
    bool IsActive = true);

public interface IPlatformCatalogService
{
    Task<PlatformCatalogSnapshot> GetCatalogAsync(CancellationToken cancellationToken);

    Task<ManagedPlanPriceResult> CreateDraftAsync(
        Guid actorUserId,
        string actorRole,
        CreatePlanPriceCommand command,
        CancellationToken cancellationToken);

    Task<ManagedPlanPriceResult> PublishAsync(
        Guid actorUserId,
        string actorRole,
        Guid priceId,
        CancellationToken cancellationToken);

    Task<ManagedPlanPriceResult> ArchiveAsync(
        Guid actorUserId,
        string actorRole,
        Guid priceId,
        CancellationToken cancellationToken);

    Task<long?> GetPublishedAmountMinorAsync(
        string productCode,
        string interval,
        string currency,
        CancellationToken cancellationToken);
}

public sealed record CreatePlanPriceCommand(
    string ProductCode,
    string Interval,
    long AmountMinor,
    string Currency = "TRY",
    bool TaxInclusive = true);

public sealed record ManagedPlanPriceResult(
    Guid Id,
    string ProductCode,
    string ProductKind,
    string DisplayName,
    string Interval,
    string Currency,
    long AmountMinor,
    bool TaxInclusive,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PublishedAtUtc,
    DateTimeOffset? ArchivedAtUtc);

public sealed record PlatformCatalogPlanResult(
    string ProductCode,
    string ProductKind,
    string DisplayName,
    FeatureEntitlements? Entitlements,
    IReadOnlyList<ManagedPlanPriceResult> Prices);

public sealed record PlatformCatalogSnapshot(
    IReadOnlyList<PlatformCatalogPlanResult> Products,
    string Note);

public interface IPlatformSubscriptionOfferService
{
    Task<IReadOnlyList<ManagedSubscriptionOfferResult>> ListAsync(CancellationToken cancellationToken);

    Task<ManagedSubscriptionOfferResult> CreateAsync(
        CreateSubscriptionOfferCommand command,
        CancellationToken cancellationToken);

    Task<ManagedSubscriptionOfferResult> SetActiveAsync(
        Guid offerId,
        bool isActive,
        CancellationToken cancellationToken);
}

public sealed record ManagedSubscriptionOfferResult(
    Guid Id,
    string Audience,
    string TargetPlanCode,
    int DiscountPercent,
    int DurationMonths,
    string Title,
    string Body,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    bool IsActive);

public sealed record CreateSubscriptionOfferCommand(
    string Audience,
    string TargetPlanCode,
    int DiscountPercent,
    int DurationMonths,
    string Title,
    string Body,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    bool IsActive = true);

public sealed record ManagedNotificationResult(
    Guid Id,
    Guid? TenantId,
    string Audience,
    string Title,
    string Body,
    string? ActionUrl,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    bool IsActive,
    DateTimeOffset? LastDispatchedAtUtc);

public sealed record CreateManagedNotificationCommand(
    string Audience,
    string Title,
    string Body,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    string? ActionUrl,
    bool IsActive = true,
    bool BroadcastToAllTenants = false);

public sealed record NotificationDispatchResult(
    int EmailSentCount,
    int PushSentCount,
    IReadOnlyList<string> RecipientEmails);


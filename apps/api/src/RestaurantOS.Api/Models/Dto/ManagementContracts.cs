namespace RestaurantOS.Api.Models.Dto;

public sealed record ManagementLoginRequest(
    string Email,
    string Password,
    Guid? TenantId = null,
    Guid? BranchId = null);

public sealed record ManagementRegisterRequest(
    string Email,
    string Password,
    string RestaurantName,
    string BranchName);

public sealed record ManagementAccessTokenResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    Guid UserId,
    Guid TenantId,
    Guid BranchId,
    string Realm = "management",
    string? RoleCode = null,
    string? Email = null);

public sealed record PlatformSessionResponse(
    Guid UserId,
    string Email,
    string RoleCode,
    string Realm);

public sealed record ManagementWorkspaceResponse(
    Guid TenantId,
    Guid RestaurantId,
    string RestaurantName,
    Guid BranchId,
    string BranchName,
    ManagementEntitlementUsageResponse? Entitlements = null,
    IReadOnlyList<ManagementAudienceNotificationResponse>? Notifications = null,
    IReadOnlyList<ManagementSubscriptionOfferResponse>? SubscriptionOffers = null,
    IReadOnlyList<string>? Permissions = null);

public sealed record ManagementAudienceNotificationResponse(
    Guid Id,
    string Title,
    string Body,
    string? ActionUrl);

public sealed record ManagementSubscriptionOfferResponse(
    Guid Id,
    string TargetPlanCode,
    int DiscountPercent,
    int DurationMonths,
    string Title,
    string Body);

public sealed record ManagementSubscriptionOfferAdminResponse(
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

public sealed record ManagementEntitlementUsageResponse(
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
    bool CanViewFinancialAnalytics = false,
    int IncludedBranches = 0,
    int PurchasedBranchAddonCount = 0,
    int FrozenBranchCount = 0,
    int ActiveBranchCount = 0,
    long ExtraBranchMonthlyPriceMinor = 0,
    string BillingCurrency = "TRY",
    bool NextBranchRequiresAddon = false);

public sealed record ManagementCloseTableCheckRequest(
    string Tender,
    bool ConfirmIncompleteKitchen = false,
    string? Note = null);

public sealed record ManagementTableCheckResponse(
    int RoundCount,
    long TotalAmountMinor,
    bool HasIncompleteKitchen);

public sealed record ManagementTableCheckCloseResponse(
    int ClosedOrderCount,
    long TotalAmountMinor,
    string Tender,
    bool ForcedIncompleteKitchen);

public sealed record ManagementChangeOrderStatusRequest(
    string Status,
    DateTimeOffset ExpectedStatusChangedAtUtc,
    DateTimeOffset? EstimatedReadyAtUtc = null);

public sealed record ManagementOrderResponse(
    Guid Id,
    string DisplayNumber,
    string Status,
    DateTimeOffset? CreatedAtUtc,
    DateTimeOffset StatusChangedAtUtc,
    DateTimeOffset EstimatedReadyAtUtc,
    long AmountMinor,
    string Currency,
    Guid TableId,
    string TableLabel);

public sealed record ManagementTodayDashboardResponse(
    int TodaysOrderCount,
    long TodaysRevenueMinor,
    int OpenTablesCount,
    int PendingOrdersCount,
    int CompletedOrdersTodayCount,
    string Currency,
    DateTimeOffset DayStartUtc,
    DateTimeOffset DayEndUtc,
    bool CanViewFinancials = false);

public sealed record ManagementOrderLineResponse(
    Guid Id,
    Guid MenuItemId,
    string Name,
    int Quantity,
    long ListUnitPriceAmountMinor,
    long DiscountUnitAmountMinor,
    long UnitPriceAmountMinor,
    string Currency,
    string? Note);

public sealed record ManagementOrderStatusHistoryResponse(
    string Status,
    DateTimeOffset ChangedAtUtc,
    Guid? ChangedByUserId);

public sealed record ManagementOrderDetailResponse(
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
    IReadOnlyList<ManagementOrderLineResponse> Items,
    IReadOnlyList<ManagementOrderStatusHistoryResponse> StatusHistory);

public sealed record ManagementServiceRequestResponse(
    Guid Id,
    Guid TableId,
    string TableLabel,
    string Type,
    string Status,
    string? Note,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record ManagementCreateTableRequest(string Label);

public sealed record ManagementUpdateTableRequest(string? Label, bool? IsActive);

public sealed record ManagementTableResponse(
    Guid Id,
    string Label,
    bool IsActive,
    int ActiveQrCount,
    string OperationalStatus);

public sealed record ManagementQrCodeResponse(
    Guid Id,
    Guid TableId,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RevokedAtUtc);

public sealed record ManagementGeneratedQrResponse(
    Guid Id,
    Guid TableId,
    string TableLabel,
    string Status,
    string Token,
    string EntryUrl,
    string SvgMarkup,
    DateTimeOffset CreatedAtUtc);

public sealed record ManagementQrPrintResponse(
    Guid Id,
    Guid TableId,
    string TableLabel,
    string Status,
    string EntryUrl,
    string SvgMarkup);

public sealed record ManagementCreateMenuRequest(string Name);

public sealed record ManagementCloneMenuRequest(Guid SourceMenuId);

public sealed record ManagementShareableMenuResponse(
    Guid Id,
    Guid SourceBranchId,
    string SourceBranchName,
    string Name,
    int CategoryCount,
    int ItemCount);

public sealed record ManagementRenameMenuRequest(string Name);

public sealed record ManagementCreateCategoryRequest(string Name, int SortOrder);

public sealed record ManagementUpdateCategoryRequest(string? Name, int? SortOrder);

public sealed record ManagementCreateMenuItemRequest(
    Guid CategoryId,
    string Name,
    string? Description,
    long AmountMinor,
    bool IsAvailable,
    int SortOrder,
    string? ImageUrl = null,
    string? ImageAlt = null,
    int? PrepTimeSeconds = null,
    MenuItemCatalogData? Catalog = null);

public sealed record ManagementUpdateMenuItemRequest(
    Guid? CategoryId,
    string? Name,
    string? Description,
    long? AmountMinor,
    bool? IsAvailable,
    int? SortOrder,
    string? ImageUrl = null,
    string? ImageAlt = null,
    int? PrepTimeSeconds = null,
    bool UpdatePrepTime = false,
    long? CostAmountMinor = null,
    bool UpdateCost = false,
    MenuItemCatalogData? Catalog = null,
    bool UpdateCatalog = false);

public sealed record ManagementCustomerMenuSettingsResponse(
    bool ShowDietaryFilters,
    IReadOnlyList<string> DietaryFilterOptions,
    bool ShowAllergenExclusions,
    IReadOnlyList<string> AllergenExclusionOptions,
    bool ShowProductNutrition,
    bool ShowProductAllergens,
    bool ShowProductModifiers,
    string? AllergenDisclaimer,
    string? AllergenMatrixUrl);

public sealed record ManagementUpdateCustomerMenuSettingsRequest(
    bool ShowDietaryFilters,
    IReadOnlyList<string> DietaryFilterOptions,
    bool ShowAllergenExclusions,
    IReadOnlyList<string> AllergenExclusionOptions,
    bool ShowProductNutrition,
    bool ShowProductAllergens,
    bool ShowProductModifiers,
    string? AllergenDisclaimer,
    string? AllergenMatrixUrl);

public sealed record ManagementStockPhotoCategoryResponse(string Id, string Label);

public sealed record ManagementStockPhotoResponse(
    string Id,
    string Category,
    string Path,
    string Title,
    string Alt,
    IReadOnlyList<string> Tags);

public sealed record ManagementStockPhotoLibraryResponse(
    IReadOnlyList<ManagementStockPhotoCategoryResponse> Categories,
    IReadOnlyList<ManagementStockPhotoResponse> Photos);

public sealed record ManagementSubscriptionCheckoutRequest(string PlanCode);

public sealed record ManagementAnalyticsSummaryResponse(
    long GrossSalesMinor,
    long? EstimatedCostMinor,
    long? GrossProfitMinor,
    long CancelledSalesMinor,
    int CompletedOrderCount,
    int CancelledOrderCount,
    int OpenServiceRequestCount,
    long AverageTicketMinor,
    string Currency,
    bool CanViewFinancials);

public sealed record ManagementSalesPeriodResponse(
    DateTimeOffset PeriodStartUtc,
    long GrossSalesMinor,
    long? GrossProfitMinor,
    int OrderCount);

public sealed record ManagementTopItemResponse(
    Guid MenuItemId,
    string Name,
    int QuantitySold,
    long RevenueMinor,
    long? EstimatedCostMinor,
    long? GrossProfitMinor);

public sealed record ManagementMenuSummaryResponse(
    Guid Id,
    string Name,
    string Lifecycle,
    DateTimeOffset? PublishedAtUtc,
    int CategoryCount,
    int ItemCount);

public sealed record ManagementMenuDetailResponse(
    Guid Id,
    string Name,
    string Lifecycle,
    DateTimeOffset? PublishedAtUtc,
    IReadOnlyList<ManagementMenuCategoryResponse> Categories,
    IReadOnlyList<ManagementMenuItemResponse> Items);

public sealed record ManagementUpsertTranslationRequest(string Name, string? Description);

public sealed record MenuTextTranslationResponse(string Locale, string Name, string Description);

public sealed record ManagementMenuCategoryResponse(
    Guid Id,
    Guid MenuId,
    string Name,
    int SortOrder,
    IReadOnlyList<MenuTextTranslationResponse> Translations);

public sealed record ManagementMenuItemResponse(
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
    IReadOnlyList<MenuTextTranslationResponse> Translations,
    int? PrepTimeSeconds = null,
    MenuItemCatalogData? Catalog = null);

public sealed record ManagementMenuPromotionResponse(
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

public sealed record ManagementCreateMenuPromotionRequest(
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

public sealed record ManagementUpdateMenuPromotionRequest(
    string? Name,
    bool? IsActive,
    DateTimeOffset? EndsAtUtc);

public sealed record ManagementManagedNotificationResponse(
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

public sealed record ManagementCreateNotificationRequest(
    string Audience,
    string Title,
    string Body,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    string? ActionUrl,
    bool IsActive = true,
    bool BroadcastToAllTenants = false);

public sealed record ManagementNotificationDispatchResponse(
    int EmailSentCount,
    int PushSentCount,
    IReadOnlyList<string> RecipientEmails);

public sealed record ManagementPlatformLoginRequest(string Email, string Password);

public sealed record ManagementCreatePlatformNotificationRequest(
    string Audience,
    string Title,
    string Body,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    string? ActionUrl,
    bool IsActive = true);

public sealed record ManagementCreateSubscriptionOfferRequest(
    string Audience,
    string TargetPlanCode,
    int DiscountPercent,
    int DurationMonths,
    string Title,
    string Body,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    bool IsActive = true);

public sealed record ManagementSetSubscriptionOfferActiveRequest(bool IsActive);

public sealed record ManagementCreatePlanPriceRequest(
    string ProductCode,
    string Interval,
    long AmountMinor,
    string Currency = "TRY",
    bool TaxInclusive = true);

public sealed record ManagementPlanPriceResponse(
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

public sealed record ManagementCatalogProductResponse(
    string ProductCode,
    string ProductKind,
    string DisplayName,
    int? MaxBranches,
    int? MaxTablesPerBranch,
    int? MaxActiveUsers,
    bool? CanUseLiveOrderPanel,
    bool? CanUseMultiBranch,
    bool? HasPrioritySupport,
    IReadOnlyList<ManagementPlanPriceResponse> Prices);

public sealed record ManagementCatalogResponse(
    IReadOnlyList<ManagementCatalogProductResponse> Products,
    string Note);

public sealed record ManagementCreateBranchRequest(string Name, bool ConfirmAddonPurchase = false);

public sealed record ManagementRenameBranchRequest(string Name);

public sealed record ManagementInviteBranchMemberRequest(
    string Email,
    string? Password = null,
    string RoleKey = "manager");

public sealed record ManagementSwitchBranchRequest(Guid BranchId);

public sealed record ManagementBranchResponse(
    Guid Id,
    Guid RestaurantId,
    string Name,
    int ActiveMemberCount,
    int ActiveTableCount,
    bool IsCurrent,
    bool IsFrozen = false);

public sealed record ManagementBranchMemberResponse(
    Guid MembershipId,
    Guid UserId,
    string Email,
    string RoleName,
    string RoleKey,
    bool IsActive,
    DateTimeOffset? LastLoginAtUtc);

public sealed record ManagementMembershipScopeResponse(
    Guid MembershipId,
    Guid TenantId,
    Guid RestaurantId,
    string RestaurantName,
    Guid BranchId,
    string BranchName,
    string RoleName,
    bool CanManageBranches);

public sealed record ManagementNetworkBranchStatResponse(
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

public sealed record ManagementNetworkSummaryResponse(
    long GrossSalesMinor,
    int CompletedOrderCount,
    int CancelledOrderCount,
    int OpenOrderCount,
    int OpenServiceRequestCount,
    int BranchCount,
    string Currency,
    IReadOnlyList<ManagementNetworkBranchStatResponse> Branches);

public sealed record ManagementBranchBillingPreviewResponse(
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

public sealed record ManagementEnterpriseQuoteRequest(
    string ContactName,
    string Email,
    string? Phone,
    int EstimatedBranchCount,
    string? Note);

public sealed record ManagementEnterpriseQuoteResponse(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    string Message);

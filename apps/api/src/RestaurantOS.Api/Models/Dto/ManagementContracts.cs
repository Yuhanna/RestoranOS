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
    bool NextBranchRequiresAddon = false,
    bool CanUseMenuThemes = false,
    bool CanUseBrandWatermark = false,
    int ActiveQrCount = 0,
    int? MaxActiveQrCodes = null,
    int? MaxConcurrentLiveSessions = null,
    bool CanUsePromotions = false,
    bool CanUseAnalytics = false,
    int MaxOrderHistoryHours = 72);

public sealed record ManagementCloseTableCheckRequest(
    string Tender,
    bool ConfirmIncompleteKitchen = false,
    string? Note = null);

public sealed record ManagementTableCheckRoundResponse(
    Guid OrderId,
    string DisplayNumber,
    string Status,
    long AmountMinor,
    string Currency,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset StatusChangedAtUtc,
    bool IsKitchenIncomplete,
    IReadOnlyList<ManagementOrderLineResponse> Items);

public sealed record ManagementTableCheckResponse(
    Guid TableId,
    string TableLabel,
    int RoundCount,
    long TotalAmountMinor,
    string Currency,
    bool HasIncompleteKitchen,
    IReadOnlyList<ManagementTableCheckRoundResponse> Rounds);

public sealed record ManagementTableCheckCloseResponse(
    Guid TableId,
    string TableLabel,
    string Tender,
    long TotalAmountMinor,
    string Currency,
    int ClosedOrderCount,
    bool ForcedIncompleteKitchen,
    DateTimeOffset ClosedAtUtc,
    IReadOnlyList<Guid> ClosedOrderIds);

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
    string TableLabel,
    string ItemSummary = "");

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
    string? Note,
    Guid? SourcePackageId = null,
    string? SourcePackageName = null);

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
    string? AllergenMatrixUrl,
    string ThemeId = "modern",
    string? LogoUrl = null,
    string? LogoAlt = null,
    bool ShowBrandWatermark = false,
    string BrandWatermarkIntensity = "soft");

public sealed record ManagementUpdateCustomerMenuSettingsRequest(
    bool ShowDietaryFilters,
    IReadOnlyList<string> DietaryFilterOptions,
    bool ShowAllergenExclusions,
    IReadOnlyList<string> AllergenExclusionOptions,
    bool ShowProductNutrition,
    bool ShowProductAllergens,
    bool ShowProductModifiers,
    string? AllergenDisclaimer,
    string? AllergenMatrixUrl,
    string? ThemeId = null,
    string? LogoUrl = null,
    string? LogoAlt = null,
    bool ShowBrandWatermark = false,
    string? BrandWatermarkIntensity = null);

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
    bool IsActive,
    byte? DaysOfWeekMask = null);

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
    bool IsActive = true,
    byte? DaysOfWeekMask = null);

public sealed record ManagementUpdateMenuPromotionRequest(
    string? Name,
    bool? IsActive,
    DateTimeOffset? EndsAtUtc);

public sealed record ManagementLunchPackageComponentRequest(
    Guid MenuItemId,
    string? SlotLabel,
    int SortOrder = 0);

public sealed record ManagementUpsertLunchPackageRequest(
    string Name,
    string? Description,
    long PriceAmountMinor,
    string? Currency,
    bool IsActive,
    int SortOrder,
    string? DailyStartLocal,
    string? DailyEndLocal,
    byte? DaysOfWeekMask,
    IReadOnlyList<ManagementLunchPackageComponentRequest>? Components);

public sealed record ManagementSetLunchPackageActiveRequest(bool IsActive);

public sealed record ManagementLunchPackageComponentResponse(
    Guid Id,
    Guid MenuItemId,
    string MenuItemName,
    string? SlotLabel,
    int SortOrder,
    long ListAmountMinor);

public sealed record ManagementLunchPackageResponse(
    Guid Id,
    string Name,
    string? Description,
    long PriceAmountMinor,
    string Currency,
    bool IsActive,
    int SortOrder,
    string? DailyStartLocal,
    string? DailyEndLocal,
    byte? DaysOfWeekMask,
    long ComponentsListTotalMinor,
    IReadOnlyList<ManagementLunchPackageComponentResponse> Components);

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

public sealed record ManagementSetPlatformNotificationActiveRequest(bool IsActive);

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

public sealed record ManagementInvitePlatformStaffRequest(
    string Email,
    string RoleCode,
    string? Password = null);

public sealed record ManagementUpdatePlatformStaffRequest(
    string? RoleCode = null,
    bool? IsActive = null);

public sealed record ManagementPlatformStaffResponse(
    Guid UserId,
    string Email,
    string RoleCode,
    bool IsActive,
    DateTimeOffset GrantedAtUtc);

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
    string DisplayName = "",
    string? Phone = null,
    string? Password = null,
    string RoleKey = "staff");

public sealed record ManagementUpdateBranchMemberRequest(
    string DisplayName,
    string? Phone = null,
    string? RoleKey = null);

public sealed record ManagementResetBranchMemberPasswordRequest(string NewPassword);

public sealed record ManagementIncidentResponse(
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

public sealed record ManagementUpdateProfileRequest(string DisplayName, string? Phone = null);

public sealed record ManagementProfileResponse(
    Guid UserId,
    string Email,
    string? DisplayName,
    string? Phone);

public sealed record ManagementChangePasswordRequest(string CurrentPassword, string NewPassword);

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
    string? DisplayName,
    string? Phone,
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
    bool CanManageBranches,
    bool CanManageMembers = false);

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

public sealed record ManagementPlatformTenantListResponse(
    IReadOnlyList<ManagementPlatformTenantListItemResponse> Items,
    int Total,
    int Skip,
    int Take);

public sealed record ManagementPlatformTenantListItemResponse(
    Guid TenantId,
    string TenantName,
    string RestaurantName,
    string PlanCode,
    bool IsTrial,
    DateTimeOffset? ExpiresAtUtc,
    int ActiveBranchCount,
    int? MaxBranches,
    int OpenQuoteCount);

public sealed record ManagementPlatformTenantDetailResponse(
    Guid TenantId,
    string TenantName,
    string RestaurantName,
    string? BillingEmail,
    string PlanCode,
    bool IsTrial,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    int PurchasedBranchAddonCount,
    int ActiveBranchCount,
    int FrozenBranchCount,
    int ActiveUserCount,
    int? MaxBranches,
    int? MaxActiveUsers,
    int MaxOrderHistoryHours,
    int? MaxActiveQrCodes,
    int? OverrideMaxBranches,
    int? OverrideMaxActiveUsers,
    int? OverrideMaxOrderHistoryHours,
    int? OverrideMaxActiveQrCodes,
    string? ContractNote);

public sealed record ManagementUpdatePlatformTenantSubscriptionRequest(
    string PlanCode,
    DateTimeOffset? ExpiresAtUtc = null,
    int PurchasedBranchAddonCount = 0,
    int? OverrideMaxBranches = null,
    int? OverrideMaxActiveUsers = null,
    int? OverrideMaxOrderHistoryHours = null,
    int? OverrideMaxActiveQrCodes = null,
    string? ContractNote = null);

public sealed record ManagementPlatformQuoteListItemResponse(
    Guid Id,
    Guid TenantId,
    string TenantName,
    int EstimatedBranchCount,
    string Status,
    DateTimeOffset CreatedAtUtc);

public sealed record ManagementPlatformQuoteDetailResponse(
    Guid Id,
    Guid TenantId,
    string TenantName,
    string ContactName,
    string Email,
    string? Phone,
    int EstimatedBranchCount,
    string? Note,
    string Status,
    DateTimeOffset CreatedAtUtc,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAtUtc,
    string? DecisionNote);

public sealed record ManagementAcceptPlatformQuoteRequest(
    int? OverrideMaxBranches = null,
    int? OverrideMaxActiveUsers = null,
    int? OverrideMaxOrderHistoryHours = null,
    int? OverrideMaxActiveQrCodes = null,
    string? ContractNote = null,
    DateTimeOffset? ExpiresAtUtc = null);

public sealed record ManagementRejectPlatformQuoteRequest(string? DecisionNote = null);

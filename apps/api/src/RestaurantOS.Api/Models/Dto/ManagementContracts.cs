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
    Guid BranchId);

public sealed record ManagementWorkspaceResponse(
    Guid TenantId,
    Guid RestaurantId,
    string RestaurantName,
    Guid BranchId,
    string BranchName,
    ManagementEntitlementUsageResponse? Entitlements = null);

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
    DateTimeOffset? TrialEndsAtUtc = null);

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
    string Currency);

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

public sealed record ManagementTableResponse(Guid Id, string Label, bool IsActive, int ActiveQrCount);

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
    int? PrepTimeSeconds = null);

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
    bool UpdatePrepTime = false);

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
    int? PrepTimeSeconds = null);

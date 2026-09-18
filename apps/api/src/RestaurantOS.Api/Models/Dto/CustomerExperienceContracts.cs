namespace RestaurantOS.Api.Models.Dto;

public sealed record ResolveQrRequest(string QrToken, string Locale = "tr");

public sealed record CustomerMenuSettingsResponse(
    bool ShowDietaryFilters,
    IReadOnlyList<string> DietaryFilterOptions,
    bool ShowAllergenExclusions,
    IReadOnlyList<string> AllergenExclusionOptions,
    bool ShowProductNutrition,
    bool ShowProductAllergens,
    bool ShowProductModifiers,
    string? AllergenDisclaimer,
    string? AllergenMatrixUrl);

public sealed record CustomerSessionResponse(
    string SessionToken,
    string RestaurantName,
    string BranchName,
    string TableLabel,
    string Locale,
    IReadOnlyList<MenuCategoryResponse> Categories,
    IReadOnlyList<MenuProductResponse> Products,
    IReadOnlyList<string> OpenServiceRequestTypes,
    IReadOnlyList<CustomerOrderResponse> ActiveOrders,
    CustomerMenuSettingsResponse CustomerMenu,
    IReadOnlyList<MenuPackageResponse> Packages);

public sealed record MenuCategoryResponse(string Id, string Name);

public sealed record MoneyResponse(long AmountMinor, string Currency);

public sealed record PriceBreakdownResponse(
    MoneyResponse List,
    MoneyResponse Discount,
    MoneyResponse Final);

public sealed record MenuProductNutritionResponse(
    int? WeightGrams,
    int? VolumeMl,
    int? CaloriesKcal,
    int? ProteinGrams,
    int? CarbsGrams,
    int? FatGrams,
    int? SugarGrams,
    int? SaltGrams);

public sealed record MenuProductModifierOptionResponse(
    string Id,
    string Name,
    MoneyResponse PriceDelta);

public sealed record MenuProductModifierGroupResponse(
    string Id,
    string Name,
    bool Required,
    int MaxSelections,
    IReadOnlyList<MenuProductModifierOptionResponse> Options);

public sealed record MenuProductPortionResponse(
    string Id,
    string Name,
    decimal PriceMultiplier);

public sealed record MenuProductResponse(
    string Id,
    string CategoryId,
    string Name,
    string Description,
    MoneyResponse Price,
    string ImageUrl,
    string ImageAlt,
    bool Available,
    string[] DietaryTags,
    string[] AllergenKeys,
    string[] MayContainAllergenKeys,
    string[] Ingredients,
    MenuProductModifierGroupResponse[] ModifierGroups,
    MenuProductPortionResponse[] Portions,
    MenuProductNutritionResponse? Nutrition,
    string? Badge,
    bool IsNew,
    int? SpiceLevel,
    bool ContainsAlcohol,
    string? ServingNote,
    string? PriceLabel,
    string? CertificationNotes,
    string[]? CustomLabels = null,
    PriceBreakdownResponse? Pricing = null,
    string? PromotionLabel = null,
    string? DiscountKind = null,
    int? DiscountValue = null,
    DateTimeOffset? PromotionEndsAtUtc = null);

public sealed record MenuPackageComponentResponse(
    string MenuItemId,
    string Name,
    string? SlotLabel,
    long ListAmountMinor,
    string ImageUrl,
    string ImageAlt,
    string Description);

public sealed record MenuPackageResponse(
    string Id,
    string Name,
    string Description,
    MoneyResponse Price,
    MoneyResponse ListPrice,
    MoneyResponse Discount,
    IReadOnlyList<MenuPackageComponentResponse> Components,
    string? DailyStartLocal,
    string? DailyEndLocal);

public sealed record CreateCustomerOrderRequest(
    string SessionToken,
    IReadOnlyList<CreateCustomerOrderLineRequest> Lines);

public sealed record CreateCustomerOrderLineRequest(
    string? ProductId,
    int Quantity,
    IReadOnlyList<string>? ModifierOptionIds,
    string? Note,
    string? PackageId = null);

public sealed record CustomerOrderResponse(
    string Id,
    string DisplayNumber,
    string Status,
    DateTimeOffset StatusChangedAt,
    DateTimeOffset EstimatedReadyAt,
    MoneyResponse Total,
    MoneyResponse? Subtotal = null,
    MoneyResponse? Discount = null);

public sealed record CreateServiceRequestRequest(string SessionToken, string Type, string? Note = null);

public sealed record CustomerServiceRequestResponse(
    string Id,
    string TableId,
    string TableLabel,
    string Type,
    string Status,
    string? Note,
    DateTimeOffset CreatedAtUtc);

using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;

namespace RestaurantOS.Api.Controllers;

internal static class MenuCatalogMapper
{
    public static MenuProductResponse ToProductResponse(MenuItemResult item, Func<string, string> mapMediaUrl) =>
        new(
            item.Id.ToString(),
            item.CategoryId.ToString(),
            item.Name,
            item.Description,
            new MoneyResponse(item.AmountMinor, item.Currency),
            mapMediaUrl(item.ImageUrl),
            string.IsNullOrWhiteSpace(item.ImageAlt) ? item.Name : item.ImageAlt,
            item.Available,
            item.Catalog?.DietaryTags.ToArray() ?? [],
            item.Catalog?.AllergenKeys.ToArray() ?? [],
            item.Catalog?.MayContainAllergenKeys.ToArray() ?? [],
            item.Catalog?.Ingredients.ToArray() ?? [],
            item.Catalog?.ModifierGroups.Select(ToModifierGroup).ToArray() ?? [],
            item.Catalog?.Portions.Select(ToPortion).ToArray() ?? [],
            ToNutrition(item.Catalog?.Nutrition),
            item.Catalog?.Badge,
            item.Catalog?.IsNew ?? false,
            item.Catalog?.SpiceLevel,
            item.Catalog?.ContainsAlcohol ?? false,
            item.Catalog?.ServingNote,
            item.Catalog?.PriceLabel,
            item.Catalog?.CertificationNotes,
            BuildPricing(item),
            item.PromotionLabel);

    public static CustomerMenuSettingsResponse ToSettingsResponse(CustomerMenuSettingsData settings) =>
        new(
            settings.ShowDietaryFilters,
            settings.DietaryFilterOptions,
            settings.ShowAllergenExclusions,
            settings.AllergenExclusionOptions,
            settings.AllergenDisclaimer,
            settings.AllergenMatrixUrl);

    public static ManagementCustomerMenuSettingsResponse ToManagementSettingsResponse(CustomerMenuSettingsData settings) =>
        new(
            settings.ShowDietaryFilters,
            settings.DietaryFilterOptions,
            settings.ShowAllergenExclusions,
            settings.AllergenExclusionOptions,
            settings.AllergenDisclaimer,
            settings.AllergenMatrixUrl);

    public static CustomerMenuSettingsData ToSettingsData(ManagementUpdateCustomerMenuSettingsRequest request) =>
        new()
        {
            ShowDietaryFilters = request.ShowDietaryFilters,
            DietaryFilterOptions = request.DietaryFilterOptions,
            ShowAllergenExclusions = request.ShowAllergenExclusions,
            AllergenExclusionOptions = request.AllergenExclusionOptions,
            AllergenDisclaimer = request.AllergenDisclaimer,
            AllergenMatrixUrl = request.AllergenMatrixUrl,
        };

    public static ManagementMenuItemResponse ToManagementItem(ManagementMenuItemResult item) =>
        new(
            item.Id,
            item.MenuId,
            item.CategoryId,
            item.Name,
            item.Description,
            item.AmountMinor,
            item.Currency,
            item.IsAvailable,
            item.SortOrder,
            item.ImageUrl,
            item.ImageAlt,
            item.Translations.Select(x => new MenuTextTranslationResponse(x.Locale, x.Name, x.Description)).ToArray(),
            item.PrepTimeSeconds,
            item.Catalog);

    /// <summary>
    /// Customer-facing media URLs stay relative so Vite / same-origin proxy works on mobile LAN.
    /// </summary>
    public static string ToCustomerMediaUrl(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var absolute)
            && absolute.AbsolutePath.StartsWith("/media", StringComparison.OrdinalIgnoreCase))
        {
            return absolute.AbsolutePath;
        }

        return imageUrl.StartsWith('/') ? imageUrl : imageUrl;
    }

    private static PriceBreakdownResponse? BuildPricing(MenuItemResult item)
    {
        var listMinor = item.ListAmountMinor > 0 ? item.ListAmountMinor : item.AmountMinor;
        var discountMinor = item.DiscountAmountMinor;
        return discountMinor > 0
            ? new PriceBreakdownResponse(
                new MoneyResponse(listMinor, item.Currency),
                new MoneyResponse(discountMinor, item.Currency),
                new MoneyResponse(item.AmountMinor, item.Currency))
            : null;
    }

    private static MenuProductModifierGroupResponse ToModifierGroup(MenuModifierGroupData group) =>
        new(
            group.Id,
            group.Name,
            group.Required,
            group.MaxSelections,
            group.Options.Select(option => new MenuProductModifierOptionResponse(
                option.Id,
                option.Name,
                new MoneyResponse(option.PriceDeltaMinor, "TRY"))).ToArray());

    private static MenuProductPortionResponse ToPortion(MenuPortionData portion) =>
        new(portion.Id, portion.Name, portion.PriceMultiplier);

    private static MenuProductNutritionResponse? ToNutrition(MenuItemNutritionData? nutrition) =>
        nutrition is null
            ? null
            : new MenuProductNutritionResponse(
                nutrition.WeightGrams,
                nutrition.VolumeMl,
                nutrition.CaloriesKcal,
                nutrition.ProteinGrams,
                nutrition.CarbsGrams,
                nutrition.FatGrams,
                nutrition.SugarGrams,
                nutrition.SaltGrams);
}

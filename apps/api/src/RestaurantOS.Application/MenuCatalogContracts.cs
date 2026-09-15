namespace RestaurantOS.Application;

public static class MenuCatalogDefaults
{
    public static readonly string[] AllAllergenKeys =
    [
        "gluten", "crustaceans", "eggs", "fish", "peanuts", "soybeans", "milk",
        "treeNuts", "celery", "mustard", "sesame", "sulphites", "lupin", "molluscs",
    ];

    public static readonly string[] AllDietaryTags =
    [
        "vegetarian", "vegan", "glutenFree", "dairyFree", "nutFree", "halal", "kosher", "jain",
    ];

    public const string DefaultAllergenDisclaimer =
        "Alerjen bilgileri reçeteye göre güncellenir. Ciddi alerjiniz varsa garsona bildirin; paylaşılan mutfakta cross-contact riski olabilir.";
}

public sealed record MenuItemCatalogData
{
    public string? Badge { get; init; }
    public bool IsNew { get; init; }
    public IReadOnlyList<string> DietaryTags { get; init; } = [];
    public IReadOnlyList<string> AllergenKeys { get; init; } = [];
    public IReadOnlyList<string> MayContainAllergenKeys { get; init; } = [];
    public IReadOnlyList<string> Ingredients { get; init; } = [];
    public MenuItemNutritionData? Nutrition { get; init; }
    public int? SpiceLevel { get; init; }
    public bool ContainsAlcohol { get; init; }
    public string? ServingNote { get; init; }
    public string? PriceLabel { get; init; }
    public string? CertificationNotes { get; init; }
    public IReadOnlyList<MenuModifierGroupData> ModifierGroups { get; init; } = [];
    public IReadOnlyList<MenuPortionData> Portions { get; init; } = [];
}

public sealed record MenuItemNutritionData
{
    public int? WeightGrams { get; init; }
    public int? VolumeMl { get; init; }
    public int? CaloriesKcal { get; init; }
    public int? ProteinGrams { get; init; }
    public int? CarbsGrams { get; init; }
    public int? FatGrams { get; init; }
    public int? SugarGrams { get; init; }
    public int? SaltGrams { get; init; }
}

public sealed record MenuModifierGroupData
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool Required { get; init; }
    public int MaxSelections { get; init; } = 1;
    public IReadOnlyList<MenuModifierOptionData> Options { get; init; } = [];
}

public sealed record MenuModifierOptionData
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public long PriceDeltaMinor { get; init; }
}

public sealed record MenuPortionData
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal PriceMultiplier { get; init; } = 1m;
}

public sealed record CustomerMenuSettingsData
{
    public bool ShowDietaryFilters { get; init; }
    public IReadOnlyList<string> DietaryFilterOptions { get; init; } = [];
    public bool ShowAllergenExclusions { get; init; }
    public IReadOnlyList<string> AllergenExclusionOptions { get; init; } = [];
    /// <summary>Show nutrition block on customer product detail. Default off.</summary>
    public bool ShowProductNutrition { get; init; }
    /// <summary>Show allergen / may-contain / ingredients on product detail. Default off.</summary>
    public bool ShowProductAllergens { get; init; }
    /// <summary>Show modifier groups and portions on product detail. Default off.</summary>
    public bool ShowProductModifiers { get; init; }
    public string? AllergenDisclaimer { get; init; }
    public string? AllergenMatrixUrl { get; init; }
    public string? ThemeId { get; init; }
    public bool ShowBrandWatermark { get; init; }
}

public interface ICustomerMenuSettingsService
{
    Task<CustomerMenuSettingsData> GetAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken);
    Task<CustomerMenuSettingsData> UpdateAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        CustomerMenuSettingsData settings,
        CancellationToken cancellationToken);
}

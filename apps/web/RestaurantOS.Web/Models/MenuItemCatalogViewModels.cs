using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace RestaurantOS.Web.Models;

public sealed class MenuItemCatalogViewModel
{
    public string? Badge { get; set; }
    public bool IsNew { get; set; }
    public bool ContainsAlcohol { get; set; }
    public string? ServingNote { get; set; }
    public string? PriceLabel { get; set; }
    public string? CertificationNotes { get; set; }
    public int? SpiceLevel { get; set; }

    [Display(Name = "İçindekiler (virgülle)")]
    public string? IngredientsText { get; set; }

    public bool DietaryVegetarian { get; set; }
    public bool DietaryVegan { get; set; }
    public bool DietaryGlutenFree { get; set; }
    public bool DietaryDairyFree { get; set; }
    public bool DietaryNutFree { get; set; }
    public bool DietaryHalal { get; set; }
    public bool DietaryKosher { get; set; }
    public bool DietaryJain { get; set; }

    /// <summary>Free-text custom labels; bound from multiple inputs named CustomLabels.</summary>
    public List<string> CustomLabels { get; set; } = [];

    public bool AllergenGluten { get; set; }
    public bool AllergenCrustaceans { get; set; }
    public bool AllergenEggs { get; set; }
    public bool AllergenFish { get; set; }
    public bool AllergenPeanuts { get; set; }
    public bool AllergenSoybeans { get; set; }
    public bool AllergenMilk { get; set; }
    public bool AllergenTreeNuts { get; set; }
    public bool AllergenCelery { get; set; }
    public bool AllergenMustard { get; set; }
    public bool AllergenSesame { get; set; }
    public bool AllergenSulphites { get; set; }
    public bool AllergenLupin { get; set; }
    public bool AllergenMolluscs { get; set; }

    public bool MayContainGluten { get; set; }
    public bool MayContainCrustaceans { get; set; }
    public bool MayContainEggs { get; set; }
    public bool MayContainFish { get; set; }
    public bool MayContainPeanuts { get; set; }
    public bool MayContainSoybeans { get; set; }
    public bool MayContainMilk { get; set; }
    public bool MayContainTreeNuts { get; set; }
    public bool MayContainCelery { get; set; }
    public bool MayContainMustard { get; set; }
    public bool MayContainSesame { get; set; }
    public bool MayContainSulphites { get; set; }
    public bool MayContainLupin { get; set; }
    public bool MayContainMolluscs { get; set; }

    public int? NutritionWeightGrams { get; set; }
    public int? NutritionVolumeMl { get; set; }
    public int? NutritionCaloriesKcal { get; set; }
    public int? NutritionProteinGrams { get; set; }
    public int? NutritionCarbsGrams { get; set; }
    public int? NutritionFatGrams { get; set; }
    public int? NutritionSugarGrams { get; set; }
    public int? NutritionSaltGrams { get; set; }

    [Display(Name = "Modifier grupları (JSON)")]
    public string? ModifierGroupsJson { get; set; }

    [Display(Name = "Porsiyonlar (JSON)")]
    public string? PortionsJson { get; set; }
}

public sealed class CustomerMenuSettingsViewModel
{
    public bool ShowDietaryFilters { get; set; }
    public bool ShowAllergenExclusions { get; set; }

    [Display(Name = "Beslenme bilgisini müşteriye göster")]
    public bool ShowProductNutrition { get; set; }

    [Display(Name = "Alerjen bilgisini müşteriye göster")]
    public bool ShowProductAllergens { get; set; }

    [Display(Name = "Ek seçenekleri müşteriye göster")]
    public bool ShowProductModifiers { get; set; }

    [Display(Name = "Beslenme filtre seçenekleri")]
    public bool FilterVegetarian { get; set; }
    public bool FilterVegan { get; set; }
    public bool FilterGlutenFree { get; set; }
    public bool FilterDairyFree { get; set; }
    public bool FilterNutFree { get; set; }
    public bool FilterHalal { get; set; }
    public bool FilterKosher { get; set; }
    public bool FilterJain { get; set; }

    [Display(Name = "Alerjen dışlama seçenekleri")]
    public bool ExcludeGluten { get; set; }
    public bool ExcludeCrustaceans { get; set; }
    public bool ExcludeEggs { get; set; }
    public bool ExcludeFish { get; set; }
    public bool ExcludePeanuts { get; set; }
    public bool ExcludeSoybeans { get; set; }
    public bool ExcludeMilk { get; set; }
    public bool ExcludeTreeNuts { get; set; }
    public bool ExcludeCelery { get; set; }
    public bool ExcludeMustard { get; set; }
    public bool ExcludeSesame { get; set; }
    public bool ExcludeSulphites { get; set; }
    public bool ExcludeLupin { get; set; }
    public bool ExcludeMolluscs { get; set; }

    [Display(Name = "Menü altı uyarı metni")]
    public string? AllergenDisclaimer { get; set; }

    [Display(Name = "Yazılı alerjen matrisi URL")]
    public string? AllergenMatrixUrl { get; set; }

    [Display(Name = "QR menü teması")]
    public string ThemeId { get; set; } = "modern";

    public string? LogoUrl { get; set; }
    public string? LogoAlt { get; set; }
    public bool ShowBrandWatermark { get; set; }
    public string BrandWatermarkIntensity { get; set; } = "soft";

    [Display(Name = "Logo dosyası")]
    public IFormFile? LogoFile { get; set; }

    public bool ClearLogo { get; set; }
}

public sealed class MenuItemCatalogApiModel
{
    public string? Badge { get; set; }
    public bool IsNew { get; set; }
    public string[]? DietaryTags { get; set; }
    public string[]? CustomLabels { get; set; }
    public string[]? AllergenKeys { get; set; }
    public string[]? MayContainAllergenKeys { get; set; }
    public string[]? Ingredients { get; set; }
    public MenuItemNutritionApiModel? Nutrition { get; set; }
    public int? SpiceLevel { get; set; }
    public bool ContainsAlcohol { get; set; }
    public string? ServingNote { get; set; }
    public string? PriceLabel { get; set; }
    public string? CertificationNotes { get; set; }
    public object[] ModifierGroups { get; set; } = [];
    public object[] Portions { get; set; } = [];
}

public sealed class MenuItemNutritionApiModel
{
    public int? WeightGrams { get; set; }
    public int? VolumeMl { get; set; }
    public int? CaloriesKcal { get; set; }
    public int? ProteinGrams { get; set; }
    public int? CarbsGrams { get; set; }
    public int? FatGrams { get; set; }
    public int? SugarGrams { get; set; }
    public int? SaltGrams { get; set; }
}

public sealed class CustomerMenuSettingsApiModel
{
    public bool ShowDietaryFilters { get; set; }
    public string[]? DietaryFilterOptions { get; set; }
    public bool ShowAllergenExclusions { get; set; }
    public string[]? AllergenExclusionOptions { get; set; }
    public bool ShowProductNutrition { get; set; }
    public bool ShowProductAllergens { get; set; }
    public bool ShowProductModifiers { get; set; }
    public string? AllergenDisclaimer { get; set; }
    public string? AllergenMatrixUrl { get; set; }
    public string? ThemeId { get; set; }
    public string? LogoUrl { get; set; }
    public string? LogoAlt { get; set; }
    public bool ShowBrandWatermark { get; set; }
    public string? BrandWatermarkIntensity { get; set; }
}

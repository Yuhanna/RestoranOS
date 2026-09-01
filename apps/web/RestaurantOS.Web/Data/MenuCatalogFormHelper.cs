using System.Text.Json;
using RestaurantOS.Web.Models;

namespace RestaurantOS.Web.Data;

public static class MenuCatalogFormHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions JsonWriteIndented = new()
    {
        WriteIndented = true,
    };

    public static MenuItemCatalogViewModel FromApi(MenuItemCatalogApiModel? catalog)
    {
        var model = new MenuItemCatalogViewModel();
        if (catalog is null)
        {
            return model;
        }

        model.Badge = catalog.Badge;
        model.IsNew = catalog.IsNew;
        model.ContainsAlcohol = catalog.ContainsAlcohol;
        model.ServingNote = catalog.ServingNote;
        model.PriceLabel = catalog.PriceLabel;
        model.CertificationNotes = catalog.CertificationNotes;
        model.SpiceLevel = catalog.SpiceLevel;
        model.IngredientsText = catalog.Ingredients is { Length: > 0 }
            ? string.Join(", ", catalog.Ingredients)
            : null;
        SetDietary(model, catalog.DietaryTags);
        SetAllergens(model, catalog.AllergenKeys, mayContain: false);
        SetAllergens(model, catalog.MayContainAllergenKeys, mayContain: true);
        if (catalog.Nutrition is not null)
        {
            model.NutritionWeightGrams = catalog.Nutrition.WeightGrams;
            model.NutritionVolumeMl = catalog.Nutrition.VolumeMl;
            model.NutritionCaloriesKcal = catalog.Nutrition.CaloriesKcal;
            model.NutritionProteinGrams = catalog.Nutrition.ProteinGrams;
            model.NutritionCarbsGrams = catalog.Nutrition.CarbsGrams;
            model.NutritionFatGrams = catalog.Nutrition.FatGrams;
            model.NutritionSugarGrams = catalog.Nutrition.SugarGrams;
            model.NutritionSaltGrams = catalog.Nutrition.SaltGrams;
        }

        if (catalog.ModifierGroups is { Length: > 0 })
        {
            model.ModifierGroupsJson = JsonSerializer.Serialize(catalog.ModifierGroups, JsonWriteIndented);
        }

        if (catalog.Portions is { Length: > 0 })
        {
            model.PortionsJson = JsonSerializer.Serialize(catalog.Portions, JsonWriteIndented);
        }

        return model;
    }

    public static bool HasCatalogContent(MenuItemCatalogApiModel catalog) =>
        !string.IsNullOrWhiteSpace(catalog.Badge)
        || catalog.IsNew
        || catalog.ContainsAlcohol
        || !string.IsNullOrWhiteSpace(catalog.ServingNote)
        || !string.IsNullOrWhiteSpace(catalog.PriceLabel)
        || !string.IsNullOrWhiteSpace(catalog.CertificationNotes)
        || catalog.SpiceLevel is not null
        || catalog.Ingredients is { Length: > 0 }
        || catalog.DietaryTags is { Length: > 0 }
        || catalog.AllergenKeys is { Length: > 0 }
        || catalog.MayContainAllergenKeys is { Length: > 0 }
        || catalog.Nutrition is not null
        || catalog.ModifierGroups is { Length: > 0 }
        || catalog.Portions is { Length: > 0 };

    public static bool TryToApi(
        MenuItemCatalogViewModel model,
        out MenuItemCatalogApiModel? catalog,
        out string? error)
    {
        try
        {
            catalog = ToApi(model);
            error = null;
            return true;
        }
        catch (JsonException exception)
        {
            catalog = null;
            error = $"Modifier veya porsiyon JSON alanı geçersiz: {exception.Message}";
            return false;
        }
    }

    public static MenuItemCatalogApiModel ToApi(MenuItemCatalogViewModel model)
    {
        object[] modifierGroups = [];
        object[] portions = [];
        if (!string.IsNullOrWhiteSpace(model.ModifierGroupsJson))
        {
            modifierGroups = JsonSerializer.Deserialize<object[]>(model.ModifierGroupsJson, JsonOptions) ?? [];
        }

        if (!string.IsNullOrWhiteSpace(model.PortionsJson))
        {
            portions = JsonSerializer.Deserialize<object[]>(model.PortionsJson, JsonOptions) ?? [];
        }

        return new MenuItemCatalogApiModel
        {
            Badge = string.IsNullOrWhiteSpace(model.Badge) ? null : model.Badge.Trim(),
            IsNew = model.IsNew,
            ContainsAlcohol = model.ContainsAlcohol,
            ServingNote = model.ServingNote,
            PriceLabel = model.PriceLabel,
            CertificationNotes = model.CertificationNotes,
            SpiceLevel = model.SpiceLevel is >= 0 and <= 3 ? model.SpiceLevel : null,
            Ingredients = ParseIngredients(model.IngredientsText),
            DietaryTags = ReadDietary(model),
            AllergenKeys = ReadAllergens(model, mayContain: false),
            MayContainAllergenKeys = ReadAllergens(model, mayContain: true),
            Nutrition = HasNutrition(model)
                ? new MenuItemNutritionApiModel
                {
                    WeightGrams = model.NutritionWeightGrams,
                    VolumeMl = model.NutritionVolumeMl,
                    CaloriesKcal = model.NutritionCaloriesKcal,
                    ProteinGrams = model.NutritionProteinGrams,
                    CarbsGrams = model.NutritionCarbsGrams,
                    FatGrams = model.NutritionFatGrams,
                    SugarGrams = model.NutritionSugarGrams,
                    SaltGrams = model.NutritionSaltGrams,
                }
                : null,
            ModifierGroups = modifierGroups,
            Portions = portions,
        };
    }

    public static CustomerMenuSettingsViewModel FromApi(CustomerMenuSettingsApiModel? settings)
    {
        var model = new CustomerMenuSettingsViewModel
        {
            ShowDietaryFilters = settings?.ShowDietaryFilters ?? false,
            ShowAllergenExclusions = settings?.ShowAllergenExclusions ?? false,
            AllergenDisclaimer = settings?.AllergenDisclaimer,
            AllergenMatrixUrl = settings?.AllergenMatrixUrl,
        };
        SetFilter(model, settings?.DietaryFilterOptions);
        SetExclusions(model, settings?.AllergenExclusionOptions);
        return model;
    }

    public static CustomerMenuSettingsApiModel ToApi(CustomerMenuSettingsViewModel model) =>
        new()
        {
            ShowDietaryFilters = model.ShowDietaryFilters,
            ShowAllergenExclusions = model.ShowAllergenExclusions,
            AllergenDisclaimer = model.AllergenDisclaimer,
            AllergenMatrixUrl = model.AllergenMatrixUrl,
            DietaryFilterOptions = ReadFilters(model),
            AllergenExclusionOptions = ReadExclusions(model),
        };

    private static string[] ParseIngredients(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static bool HasNutrition(MenuItemCatalogViewModel model) =>
        model.NutritionWeightGrams is not null
        || model.NutritionVolumeMl is not null
        || model.NutritionCaloriesKcal is not null
        || model.NutritionProteinGrams is not null
        || model.NutritionCarbsGrams is not null
        || model.NutritionFatGrams is not null
        || model.NutritionSugarGrams is not null
        || model.NutritionSaltGrams is not null;

    private static void SetDietary(MenuItemCatalogViewModel model, string[]? tags)
    {
        if (tags is null) return;
        model.DietaryVegetarian = tags.Contains("vegetarian");
        model.DietaryVegan = tags.Contains("vegan");
        model.DietaryGlutenFree = tags.Contains("glutenFree");
        model.DietaryDairyFree = tags.Contains("dairyFree");
        model.DietaryNutFree = tags.Contains("nutFree");
        model.DietaryHalal = tags.Contains("halal");
        model.DietaryKosher = tags.Contains("kosher");
        model.DietaryJain = tags.Contains("jain");
    }

    private static string[] ReadDietary(MenuItemCatalogViewModel model)
    {
        var tags = new List<string>();
        if (model.DietaryVegetarian) tags.Add("vegetarian");
        if (model.DietaryVegan) tags.Add("vegan");
        if (model.DietaryGlutenFree) tags.Add("glutenFree");
        if (model.DietaryDairyFree) tags.Add("dairyFree");
        if (model.DietaryNutFree) tags.Add("nutFree");
        if (model.DietaryHalal) tags.Add("halal");
        if (model.DietaryKosher) tags.Add("kosher");
        if (model.DietaryJain) tags.Add("jain");
        return tags.ToArray();
    }

    private static void SetAllergens(MenuItemCatalogViewModel model, string[]? keys, bool mayContain)
    {
        if (keys is null) return;
        foreach (var key in keys)
        {
            SetAllergenFlag(model, key, mayContain, true);
        }
    }

    private static string[] ReadAllergens(MenuItemCatalogViewModel model, bool mayContain)
    {
        var keys = new[]
        {
            "gluten", "crustaceans", "eggs", "fish", "peanuts", "soybeans", "milk",
            "treeNuts", "celery", "mustard", "sesame", "sulphites", "lupin", "molluscs",
        };
        return keys.Where(key => GetAllergenFlag(model, key, mayContain)).ToArray();
    }

    private static void SetAllergenFlag(MenuItemCatalogViewModel model, string key, bool mayContain, bool value)
    {
        switch (key)
        {
            case "gluten": if (mayContain) model.MayContainGluten = value; else model.AllergenGluten = value; break;
            case "crustaceans": if (mayContain) model.MayContainCrustaceans = value; else model.AllergenCrustaceans = value; break;
            case "eggs": if (mayContain) model.MayContainEggs = value; else model.AllergenEggs = value; break;
            case "fish": if (mayContain) model.MayContainFish = value; else model.AllergenFish = value; break;
            case "peanuts": if (mayContain) model.MayContainPeanuts = value; else model.AllergenPeanuts = value; break;
            case "soybeans": if (mayContain) model.MayContainSoybeans = value; else model.AllergenSoybeans = value; break;
            case "milk": if (mayContain) model.MayContainMilk = value; else model.AllergenMilk = value; break;
            case "treeNuts": if (mayContain) model.MayContainTreeNuts = value; else model.AllergenTreeNuts = value; break;
            case "celery": if (mayContain) model.MayContainCelery = value; else model.AllergenCelery = value; break;
            case "mustard": if (mayContain) model.MayContainMustard = value; else model.AllergenMustard = value; break;
            case "sesame": if (mayContain) model.MayContainSesame = value; else model.AllergenSesame = value; break;
            case "sulphites": if (mayContain) model.MayContainSulphites = value; else model.AllergenSulphites = value; break;
            case "lupin": if (mayContain) model.MayContainLupin = value; else model.AllergenLupin = value; break;
            case "molluscs": if (mayContain) model.MayContainMolluscs = value; else model.AllergenMolluscs = value; break;
        }
    }

    private static bool GetAllergenFlag(MenuItemCatalogViewModel model, string key, bool mayContain) =>
        key switch
        {
            "gluten" => mayContain ? model.MayContainGluten : model.AllergenGluten,
            "crustaceans" => mayContain ? model.MayContainCrustaceans : model.AllergenCrustaceans,
            "eggs" => mayContain ? model.MayContainEggs : model.AllergenEggs,
            "fish" => mayContain ? model.MayContainFish : model.AllergenFish,
            "peanuts" => mayContain ? model.MayContainPeanuts : model.AllergenPeanuts,
            "soybeans" => mayContain ? model.MayContainSoybeans : model.AllergenSoybeans,
            "milk" => mayContain ? model.MayContainMilk : model.AllergenMilk,
            "treeNuts" => mayContain ? model.MayContainTreeNuts : model.AllergenTreeNuts,
            "celery" => mayContain ? model.MayContainCelery : model.AllergenCelery,
            "mustard" => mayContain ? model.MayContainMustard : model.AllergenMustard,
            "sesame" => mayContain ? model.MayContainSesame : model.AllergenSesame,
            "sulphites" => mayContain ? model.MayContainSulphites : model.AllergenSulphites,
            "lupin" => mayContain ? model.MayContainLupin : model.AllergenLupin,
            "molluscs" => mayContain ? model.MayContainMolluscs : model.AllergenMolluscs,
            _ => false,
        };

    private static void SetFilter(CustomerMenuSettingsViewModel model, string[]? options)
    {
        if (options is null) return;
        model.FilterVegetarian = options.Contains("vegetarian");
        model.FilterVegan = options.Contains("vegan");
        model.FilterGlutenFree = options.Contains("glutenFree");
        model.FilterDairyFree = options.Contains("dairyFree");
        model.FilterNutFree = options.Contains("nutFree");
        model.FilterHalal = options.Contains("halal");
        model.FilterKosher = options.Contains("kosher");
        model.FilterJain = options.Contains("jain");
    }

    private static string[] ReadFilters(CustomerMenuSettingsViewModel model)
    {
        var tags = new List<string>();
        if (model.FilterVegetarian) tags.Add("vegetarian");
        if (model.FilterVegan) tags.Add("vegan");
        if (model.FilterGlutenFree) tags.Add("glutenFree");
        if (model.FilterDairyFree) tags.Add("dairyFree");
        if (model.FilterNutFree) tags.Add("nutFree");
        if (model.FilterHalal) tags.Add("halal");
        if (model.FilterKosher) tags.Add("kosher");
        if (model.FilterJain) tags.Add("jain");
        return tags.ToArray();
    }

    private static void SetExclusions(CustomerMenuSettingsViewModel model, string[]? options)
    {
        if (options is null) return;
        model.ExcludeGluten = options.Contains("gluten");
        model.ExcludeCrustaceans = options.Contains("crustaceans");
        model.ExcludeEggs = options.Contains("eggs");
        model.ExcludeFish = options.Contains("fish");
        model.ExcludePeanuts = options.Contains("peanuts");
        model.ExcludeSoybeans = options.Contains("soybeans");
        model.ExcludeMilk = options.Contains("milk");
        model.ExcludeTreeNuts = options.Contains("treeNuts");
        model.ExcludeCelery = options.Contains("celery");
        model.ExcludeMustard = options.Contains("mustard");
        model.ExcludeSesame = options.Contains("sesame");
        model.ExcludeSulphites = options.Contains("sulphites");
        model.ExcludeLupin = options.Contains("lupin");
        model.ExcludeMolluscs = options.Contains("molluscs");
    }

    private static string[] ReadExclusions(CustomerMenuSettingsViewModel model)
    {
        var keys = new List<string>();
        if (model.ExcludeGluten) keys.Add("gluten");
        if (model.ExcludeCrustaceans) keys.Add("crustaceans");
        if (model.ExcludeEggs) keys.Add("eggs");
        if (model.ExcludeFish) keys.Add("fish");
        if (model.ExcludePeanuts) keys.Add("peanuts");
        if (model.ExcludeSoybeans) keys.Add("soybeans");
        if (model.ExcludeMilk) keys.Add("milk");
        if (model.ExcludeTreeNuts) keys.Add("treeNuts");
        if (model.ExcludeCelery) keys.Add("celery");
        if (model.ExcludeMustard) keys.Add("mustard");
        if (model.ExcludeSesame) keys.Add("sesame");
        if (model.ExcludeSulphites) keys.Add("sulphites");
        if (model.ExcludeLupin) keys.Add("lupin");
        if (model.ExcludeMolluscs) keys.Add("molluscs");
        return keys.ToArray();
    }
}

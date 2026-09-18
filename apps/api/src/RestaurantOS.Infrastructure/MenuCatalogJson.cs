using System.Text.Json;
using System.Text.Json.Serialization;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public static class MenuCatalogJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static MenuItemCatalogData ParseItem(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new MenuItemCatalogData();
        }

        try
        {
            return JsonSerializer.Deserialize<MenuItemCatalogData>(json, Options) ?? new MenuItemCatalogData();
        }
        catch (JsonException)
        {
            return new MenuItemCatalogData();
        }
    }

    public static string SerializeItem(MenuItemCatalogData data) =>
        JsonSerializer.Serialize(NormalizeItem(data), Options);

    public static CustomerMenuSettingsData ParseSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return DefaultSettings();
        }

        try
        {
            return NormalizeSettings(
                JsonSerializer.Deserialize<CustomerMenuSettingsData>(json, Options) ?? DefaultSettings());
        }
        catch (JsonException)
        {
            return DefaultSettings();
        }
    }

    public static string SerializeSettings(CustomerMenuSettingsData data) =>
        JsonSerializer.Serialize(NormalizeSettings(data), Options);

    public static CustomerMenuSettingsData DefaultSettings() => new();

    private static MenuItemCatalogData NormalizeItem(MenuItemCatalogData data) =>
        new()
        {
            Badge = TrimOrNull(data.Badge),
            IsNew = data.IsNew,
            DietaryTags = FilterKnown(data.DietaryTags, MenuCatalogDefaults.AllDietaryTags),
            CustomLabels = NormalizeCustomLabels(data.CustomLabels),
            AllergenKeys = FilterKnown(data.AllergenKeys, MenuCatalogDefaults.AllAllergenKeys),
            MayContainAllergenKeys = FilterKnown(data.MayContainAllergenKeys, MenuCatalogDefaults.AllAllergenKeys),
            Ingredients = data.Ingredients
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Nutrition = data.Nutrition,
            SpiceLevel = data.SpiceLevel is >= 0 and <= 3 ? data.SpiceLevel : null,
            ContainsAlcohol = data.ContainsAlcohol,
            ServingNote = TrimOrNull(data.ServingNote),
            PriceLabel = TrimOrNull(data.PriceLabel),
            CertificationNotes = TrimOrNull(data.CertificationNotes),
            ModifierGroups = data.ModifierGroups
                .Where(group => !string.IsNullOrWhiteSpace(group.Id) && !string.IsNullOrWhiteSpace(group.Name))
                .Select(group => group with
                {
                    Id = group.Id.Trim(),
                    Name = group.Name.Trim(),
                    MaxSelections = Math.Max(1, group.MaxSelections),
                    Options = group.Options
                        .Where(option => !string.IsNullOrWhiteSpace(option.Id) && !string.IsNullOrWhiteSpace(option.Name))
                        .Select(option => option with
                        {
                            Id = option.Id.Trim(),
                            Name = option.Name.Trim(),
                            PriceDeltaMinor = Math.Max(0, option.PriceDeltaMinor),
                        })
                        .ToArray(),
                })
                .ToArray(),
            Portions = data.Portions
                .Where(portion => !string.IsNullOrWhiteSpace(portion.Id) && !string.IsNullOrWhiteSpace(portion.Name))
                .Select(portion => portion with
                {
                    Id = portion.Id.Trim(),
                    Name = portion.Name.Trim(),
                    PriceMultiplier = portion.PriceMultiplier <= 0 ? 1m : portion.PriceMultiplier,
                })
                .ToArray(),
        };

    private static string[] NormalizeCustomLabels(IReadOnlyList<string> values) =>
        values
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Length > MenuCatalogDefaults.MaxCustomLabelLength
                ? x[..MenuCatalogDefaults.MaxCustomLabelLength].Trim()
                : x)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MenuCatalogDefaults.MaxCustomLabels)
            .ToArray();

    private static CustomerMenuSettingsData NormalizeSettings(CustomerMenuSettingsData data) =>
        new()
        {
            ShowDietaryFilters = data.ShowDietaryFilters,
            DietaryFilterOptions = FilterKnown(data.DietaryFilterOptions, MenuCatalogDefaults.AllDietaryTags),
            ShowAllergenExclusions = data.ShowAllergenExclusions,
            AllergenExclusionOptions = FilterKnown(data.AllergenExclusionOptions, MenuCatalogDefaults.AllAllergenKeys),
            ShowProductNutrition = data.ShowProductNutrition,
            ShowProductAllergens = data.ShowProductAllergens,
            ShowProductModifiers = data.ShowProductModifiers,
            AllergenDisclaimer = SanitizeDisclaimer(TrimOrNull(data.AllergenDisclaimer))
                ?? MenuCatalogDefaults.DefaultAllergenDisclaimer,
            AllergenMatrixUrl = TrimOrNull(data.AllergenMatrixUrl),
            ThemeId = CustomerMenuThemes.Normalize(data.ThemeId),
            LogoUrl = NormalizeBrandingMediaPath(data.LogoUrl),
            LogoAlt = TrimOrNull(data.LogoAlt),
            ShowBrandWatermark = data.ShowBrandWatermark,
            BrandWatermarkIntensity = BrandWatermarkIntensities.Normalize(data.BrandWatermarkIntensity),
        };

    private static string? SanitizeDisclaimer(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (string.Equals(value, "Menü altı uyarı metni", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Allergen disclaimer", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value;
    }

    /// <summary>Only locally served media paths are stored so the logo cannot point at a remote origin.</summary>
    private static string? NormalizeBrandingMediaPath(string? value)
    {
        var trimmed = TrimOrNull(value);
        return trimmed is not null && trimmed.StartsWith("/media/", StringComparison.Ordinal)
            ? trimmed
            : null;
    }

    private static string[] FilterKnown(IReadOnlyList<string> values, IReadOnlyList<string> allowed) =>
        values
            .Select(x => x.Trim())
            .Where(x => allowed.Contains(x, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

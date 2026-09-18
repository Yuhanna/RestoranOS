namespace RestaurantOS.Domain;

/// <summary>Curated guest QR menu visual themes (branch-level).</summary>
public static class CustomerMenuThemes
{
    public const string Modern = "modern";
    public const string Luxury = "luxury";
    public const string Cafe = "cafe";
    public const string Bar = "bar";
    public const string Meyhane = "meyhane";
    public const string FastCasual = "fast_casual";
    public const string Minimal = "minimal";
    public const string Seafood = "seafood";
    public const string Grill = "grill";
    public const string Brunch = "brunch";
    public const string Asian = "asian";
    public const string Hotel = "hotel";
    public const string Healthy = "healthy";

    public static readonly IReadOnlyList<string> All =
    [
        Modern,
        Luxury,
        Cafe,
        Bar,
        Meyhane,
        FastCasual,
        Minimal,
        Seafood,
        Grill,
        Brunch,
        Asian,
        Hotel,
        Healthy,
    ];

    public static string Normalize(string? themeId)
    {
        var value = (themeId ?? string.Empty).Trim().ToLowerInvariant();
        return All.Contains(value, StringComparer.Ordinal) ? value : Modern;
    }

    /// <summary>Free plan may only serve the default theme; Pro/Trial unlock the rest.</summary>
    public static string ResolveEffective(string? themeId, bool canUseMenuThemes)
    {
        var normalized = Normalize(themeId);
        if (!canUseMenuThemes && normalized != Modern)
        {
            return Modern;
        }

        return normalized;
    }

    public static bool IsPremium(string? themeId) => Normalize(themeId) != Modern;
}

/// <summary>Faint branch-logo background on the guest QR menu (branch-level).</summary>
public static class BrandWatermarkIntensities
{
    public const string Soft = "soft";
    public const string Medium = "medium";

    public static readonly IReadOnlyList<string> All = [Soft, Medium];

    public static string Normalize(string? intensity)
    {
        var value = (intensity ?? string.Empty).Trim().ToLowerInvariant();
        return All.Contains(value, StringComparer.Ordinal) ? value : Soft;
    }
}

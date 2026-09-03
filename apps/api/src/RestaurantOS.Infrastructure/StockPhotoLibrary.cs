using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using RestaurantOS.Application;

namespace RestaurantOS.Infrastructure;

public sealed class StockPhotoLibrary(IHostEnvironment environment) : IStockPhotoLibrary
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private StockManifest? _cached;

    public StockPhotoLibraryResult List(string? query, string? categoryId)
    {
        var manifest = _cached ??= LoadManifest();
        var normalizedQuery = NormalizeSearchText(query);
        var normalizedCategory = NormalizeSearchText(categoryId);
        if (normalizedCategory is "all")
        {
            normalizedCategory = null;
        }
        if (normalizedCategory is "all" or "")
        {
            normalizedCategory = null;
        }

        var photos = manifest.Photos
            .Where(photo => File.Exists(ResolveAbsolutePath(photo.File)))
            .Where(photo => normalizedCategory is null || photo.Category.Equals(normalizedCategory, StringComparison.OrdinalIgnoreCase))
            .Where(photo =>
                normalizedQuery is null
                || ContainsNormalized(photo.Title, normalizedQuery)
                || ContainsNormalized(photo.Alt, normalizedQuery)
                || photo.Tags.Any(tag => ContainsNormalized(tag, normalizedQuery)))
            .Select(photo => new StockPhotoResult(
                photo.Id,
                photo.Category,
                $"/media/stock/{photo.File.Replace('\\', '/')}",
                photo.Title,
                photo.Alt,
                photo.Tags))
            .ToArray();

        return new StockPhotoLibraryResult(manifest.Categories, photos);
    }

    private static string? NormalizeSearchText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR")).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static bool ContainsNormalized(string haystack, string needle) =>
        NormalizeSearchText(haystack)?.Contains(needle, StringComparison.Ordinal) ?? false;

    private string ResolveAbsolutePath(string relativeFile) =>
        Path.Combine(environment.ContentRootPath, "media", "stock", relativeFile.Replace('/', Path.DirectorySeparatorChar));

    private StockManifest LoadManifest()
    {
        var manifestPath = Path.Combine(environment.ContentRootPath, "media", "stock", "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return new StockManifest([], []);
        }

        var json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize<StockManifest>(json, JsonOptions) ?? new StockManifest([], []);
    }

    private sealed record StockManifest(
        IReadOnlyList<StockPhotoCategoryResult> Categories,
        IReadOnlyList<StockManifestPhoto> Photos);

    private sealed record StockManifestPhoto(
        string Id,
        string Category,
        string File,
        string Title,
        string Alt,
        IReadOnlyList<string> Tags);
}

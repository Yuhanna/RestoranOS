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
        var normalizedQuery = query?.Trim().ToLowerInvariant();
        var normalizedCategory = categoryId?.Trim().ToLowerInvariant();
        if (normalizedCategory is "all" or "")
        {
            normalizedCategory = null;
        }

        var photos = manifest.Photos
            .Where(photo => File.Exists(ResolveAbsolutePath(photo.File)))
            .Where(photo => normalizedCategory is null || photo.Category.Equals(normalizedCategory, StringComparison.OrdinalIgnoreCase))
            .Where(photo =>
                normalizedQuery is null
                || photo.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || photo.Alt.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                || photo.Tags.Any(tag => tag.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)))
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

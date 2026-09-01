namespace RestaurantOS.Application;

public sealed record StockPhotoCategoryResult(string Id, string Label);

public sealed record StockPhotoResult(
    string Id,
    string Category,
    string Path,
    string Title,
    string Alt,
    IReadOnlyList<string> Tags);

public sealed record StockPhotoLibraryResult(
    IReadOnlyList<StockPhotoCategoryResult> Categories,
    IReadOnlyList<StockPhotoResult> Photos);

public interface IStockPhotoLibrary
{
    StockPhotoLibraryResult List(string? query, string? categoryId);
}

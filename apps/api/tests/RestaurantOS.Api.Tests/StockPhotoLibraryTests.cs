using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Tests;

public sealed class StockPhotoLibraryTests
{
    [Fact]
    public void ListFiltersByProductNameTokens()
    {
        var contentRoot = ResolveApiContentRoot();
        var library = new StockPhotoLibrary(new TestHostEnvironment(contentRoot));

        var result = library.List("levrek", null);

        Assert.Contains(result.Photos, photo => photo.Id == "izgara-levrek");
    }

    [Fact]
    public void ListReturnsEmptyForUnknownCategory()
    {
        var contentRoot = ResolveApiContentRoot();
        var library = new StockPhotoLibrary(new TestHostEnvironment(contentRoot));

        var result = library.List(null, "unknown");

        Assert.Empty(result.Photos);
    }

    [Fact]
    public void ListMatchesTurkishCharactersWithoutDiacritics()
    {
        var contentRoot = ResolveApiContentRoot();
        var library = new StockPhotoLibrary(new TestHostEnvironment(contentRoot));

        var result = library.List("kofte", null);

        Assert.Contains(result.Photos, photo => photo.Id == "kofte");
    }

    private static string ResolveApiContentRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "RestaurantOS.Api");
            if (Directory.Exists(Path.Combine(candidate, "media", "stock")))
            {
                return candidate;
            }

            candidate = Path.Combine(directory.FullName, "apps", "api", "src", "RestaurantOS.Api");
            if (Directory.Exists(Path.Combine(candidate, "media", "stock")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("RestaurantOS.Api content root with stock media was not found.");
    }

    private sealed class TestHostEnvironment(string contentRoot) : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Microsoft.Extensions.Hosting.Environments.Development;

        public string ApplicationName { get; set; } = "RestaurantOS.Api.Tests";

        public string ContentRootPath { get; set; } = contentRoot;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Tests;

public sealed class MenuCatalogJsonTests
{
    [Fact]
    public void ParseItemReturnsEmptyCatalogForInvalidJson()
    {
        var parsed = MenuCatalogJson.ParseItem("{ this is not valid json");

        Assert.NotNull(parsed);
        Assert.Empty(parsed.DietaryTags);
        Assert.Empty(parsed.ModifierGroups);
    }

    [Fact]
    public void ParseItemRoundTripsKnownFields()
    {
        var original = MenuCatalogJson.SerializeItem(new()
        {
            Badge = "Yeni",
            DietaryTags = ["vegetarian"],
            Ingredients = ["domates", "peynir"],
        });

        var parsed = MenuCatalogJson.ParseItem(original);

        Assert.Equal("Yeni", parsed.Badge);
        Assert.Contains("vegetarian", parsed.DietaryTags);
        Assert.Equal(["domates", "peynir"], parsed.Ingredients);
    }
}

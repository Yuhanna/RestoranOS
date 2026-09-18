using RestaurantOS.Domain;
using Xunit;

namespace RestaurantOS.Api.Tests;

public sealed class CustomerMenuThemesTests
{
    [Theory]
    [InlineData(null, "modern")]
    [InlineData("", "modern")]
    [InlineData("Cafe", "cafe")]
    [InlineData("luxury", "luxury")]
    [InlineData("MINIMAL", "minimal")]
    [InlineData("seafood", "seafood")]
    [InlineData("GRILL", "grill")]
    [InlineData("brunch", "brunch")]
    [InlineData("asian", "asian")]
    [InlineData("hotel", "hotel")]
    [InlineData("healthy", "healthy")]
    [InlineData("FAST_CASUAL", "fast_casual")]
    [InlineData("unknown", "modern")]
    public void NormalizeFallsBackToModern(string? input, string expected) =>
        Assert.Equal(expected, CustomerMenuThemes.Normalize(input));

    [Fact]
    public void ResolveEffectiveCoercesPremiumWhenNotEntitled()
    {
        Assert.Equal("modern", CustomerMenuThemes.ResolveEffective("bar", canUseMenuThemes: false));
        Assert.Equal("bar", CustomerMenuThemes.ResolveEffective("bar", canUseMenuThemes: true));
        Assert.Equal("luxury", CustomerMenuThemes.ResolveEffective("luxury", canUseMenuThemes: true));
        Assert.Equal("modern", CustomerMenuThemes.ResolveEffective("seafood", canUseMenuThemes: false));
        Assert.Equal("grill", CustomerMenuThemes.ResolveEffective("grill", canUseMenuThemes: true));
        Assert.Equal("modern", CustomerMenuThemes.ResolveEffective("modern", canUseMenuThemes: false));
    }

    [Fact]
    public void IsPremiumOnlyNonModern()
    {
        Assert.False(CustomerMenuThemes.IsPremium("modern"));
        Assert.True(CustomerMenuThemes.IsPremium("meyhane"));
        Assert.True(CustomerMenuThemes.IsPremium("luxury"));
        Assert.True(CustomerMenuThemes.IsPremium("minimal"));
        Assert.True(CustomerMenuThemes.IsPremium("brunch"));
        Assert.True(CustomerMenuThemes.IsPremium("asian"));
        Assert.True(CustomerMenuThemes.IsPremium("hotel"));
        Assert.True(CustomerMenuThemes.IsPremium("healthy"));
    }

    [Fact]
    public void CatalogContainsThirteenThemes() =>
        Assert.Equal(13, CustomerMenuThemes.All.Count);
}

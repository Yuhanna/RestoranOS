using RestaurantOS.Admin.Data;

namespace RestaurantOS.Admin.Tests;

public sealed class AdminMoneyTests
{
    [Fact]
    public void FreePlanIsAlwaysZero()
    {
        Assert.True(AdminMoney.TryToMinorUnits("Free", "999", out var amount, out var error));
        Assert.Equal(0, amount);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("2499", 249900)]
    [InlineData("2499,50", 249950)]
    [InlineData("2.499,50", 249950)]
    [InlineData("12.34", 1234)]
    [InlineData("0", 0)]
    public void ConvertsLiraToMinorUnits(string text, long expected)
    {
        Assert.True(AdminMoney.TryToMinorUnits("Pro", text, out var amount, out var error));
        Assert.Equal(expected, amount);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("Pro", "")]
    [InlineData("Pro", "abc")]
    [InlineData("Pro", "-1")]
    public void RejectsInvalidAmounts(string product, string text)
    {
        Assert.False(AdminMoney.TryToMinorUnits(product, text, out _, out var error));
        Assert.Equal("Geçerli bir tutar girin.", error);
    }
}

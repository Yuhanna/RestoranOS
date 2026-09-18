using RestaurantOS.Admin.Data;

namespace RestaurantOS.Admin.Tests;

public sealed class AdminLocalTimeTests
{
    [Fact]
    public void EmptyTextIsNullUtc()
    {
        Assert.True(AdminLocalTime.TryParseOptional("  ", out var utc, out var error));
        Assert.Null(utc);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void InvalidTextFails()
    {
        Assert.False(AdminLocalTime.TryParseOptional("not-a-date", out var utc, out var error));
        Assert.Null(utc);
        Assert.Equal("Tarih geçersiz.", error);
    }
}
